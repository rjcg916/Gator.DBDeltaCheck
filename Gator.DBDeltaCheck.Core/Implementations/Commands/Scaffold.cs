using CommandLine;
using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace Gator.DBDeltaCheck.Core.Implementations.Commands;
public partial class CommandLineHandler
{
    [Verb("scaffold", HelpText = "Generates a populated seed file from existing database data.")]
    public class ScaffoldOptions
    {
        [Option("template-file", Required = true, HelpText = "Path to the template JSON file.")]
        public string TemplateFile { get; set; }

        [Option("map-file", Required = true, HelpText = "Path to the map JSON file.")]
        public string MapFile { get; set; }

        [Option("root-table", Required = true, HelpText = "Name of the root database table.")]
        public string RootTable { get; set; }

        [Option("keys", Required = false, SetName = "keysource", HelpText = "Comma-separated list of root keys.")]
        public IEnumerable<string> Keys { get; set; }

        [Option("keys-file", Required = false, SetName = "keysource", HelpText = "Path to a file containing root keys, one per line.")]
        public string KeysFile { get; set; }

        [Option("output-mode", Default = "Multiple", HelpText = "Output mode: 'Single' or 'Multiple'.")]
        public string OutputMode { get; set; }

        [Option("output-path", HelpText = "Directory for the output files. Defaults to the current directory.")]
        public string OutputPath { get; set; }

        [Option('e', "exclude-defaults", Default = false, HelpText = "Exclude columns with default values.")]
        public bool ExcludeDefaults { get; set; }

        [Option("generate-test-case", Default = false, HelpText = "Generate a basic .test.json file alongside the seed files. Only works with 'Single' output mode.")]
        public bool GenerateTestCase { get; set; }
    }

    private async Task<int> RunScaffolder(ScaffoldOptions opts)
    {
        var scaffolder = host.Services.GetRequiredService<HierarchyScaffolder>();
        var schemaService = host.Services.GetRequiredService<IDbSchemaService>();

        var templateContent = await File.ReadAllTextAsync(opts.TemplateFile);
        var templateJson = JObject.Parse(templateContent);
        var templateData = (templateJson["data"] as JArray)?.FirstOrDefault() as JObject
                           ?? throw new InvalidOperationException("Template file must have a 'data' array with at least one object.");

        var outputPath = string.IsNullOrEmpty(opts.OutputPath) ? Directory.GetCurrentDirectory() : opts.OutputPath;
        Directory.CreateDirectory(outputPath);

        var pkType = await schemaService.GetPrimaryKeyTypeAsync(opts.RootTable);

        var rootKeys = await GetRootKeysAsync(opts, pkType);
        if (!rootKeys.Any())
        {
            await Console.Error.WriteLineAsync("ERROR: No valid keys were provided. One of --keys or --keys-file must be specified and contain values.");
            return 1;
        }

        var mapContent = await File.ReadAllTextAsync(opts.MapFile);
        var dataMap = JsonConvert.DeserializeObject<DataMap>(mapContent);

        var lookupTemplates = await LoadLookupTemplatesAsync(dataMap, Path.GetDirectoryName(opts.TemplateFile));

        Console.WriteLine($"Scaffolding {rootKeys.Count} records from root table '{opts.RootTable}'...");

        var (hierarchicalData, lookupData) = await scaffolder.Scaffold(opts.RootTable, rootKeys, dataMap, templateData, lookupTemplates, opts.ExcludeDefaults);

        if (!hierarchicalData.Any() && rootKeys.Any())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Warning: No hierarchical data was scaffolded. Please verify that the provided keys exist in the '{opts.RootTable}' table.");
            Console.ResetColor();
        }

        string mainSeedFile = null;

        if (opts.OutputMode.Equals("Single", StringComparison.OrdinalIgnoreCase))
        {
            if (hierarchicalData.Any())
            {
                mainSeedFile = $"{opts.RootTable}_seed.json";
                var finalJson = new JObject { ["rootTable"] = opts.RootTable, ["data"] = new JArray(hierarchicalData) };
                var seedFilePath = Path.Combine(outputPath, mainSeedFile);
                await File.WriteAllTextAsync(seedFilePath, finalJson.ToString(Formatting.Indented));
                Console.WriteLine($" -> Saved hierarchical seed file: {seedFilePath}");
            }
        }
        else
        {
            for (var i = 0; i < hierarchicalData.Count; i++)
            {
                var finalJson = new JObject { ["rootTable"] = opts.RootTable, ["data"] = new JArray(hierarchicalData[i]) };
                var safeKey = string.Join("_", rootKeys[i].ToString().Split(Path.GetInvalidFileNameChars()));
                var seedFileName = $"{opts.RootTable}_{safeKey}_seed.json";
                var seedFilePath = Path.Combine(outputPath, seedFileName);
                await File.WriteAllTextAsync(seedFilePath, finalJson.ToString(Formatting.Indented));
                Console.WriteLine($" -> Saved hierarchical seed file: {seedFilePath}");
            }
        }

        if (lookupData.Any())
        {
            Console.WriteLine("Saving lookup table seed files...");
            foreach (var entry in lookupData)
            {
                var tableName = entry.Key;
                var lookupDataArray = entry.Value;
                var lookupSeedFilePath = Path.Combine(outputPath, $"{tableName}_seed.json");
                await File.WriteAllTextAsync(lookupSeedFilePath, lookupDataArray.ToString(Formatting.Indented));
                Console.WriteLine($" -> Saved lookup seed file: {lookupSeedFilePath}");
            }
        }

        if (opts.GenerateTestCase)
        {
            if (mainSeedFile != null)
            {
                await GenerateTestCaseFileAsync(outputPath, opts.RootTable, lookupData.Keys, mainSeedFile);
            }
            else
            {
                Console.WriteLine("Warning: Test case generation is only supported for 'Single' output mode.");
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Scaffolding complete. Hierarchical records: {hierarchicalData.Count}. Lookup tables: {lookupData.Count}.");
        Console.ResetColor();
        return 0; // Success
    }

    private static async Task GenerateTestCaseFileAsync(string outputPath, string rootTable, IEnumerable<string> lookupTableNames, string hierarchicalSeedFileName)
    {
        var arrangeSteps = new JArray();

        // Add lookup table seeds first, sorted alphabetically for consistent output
        foreach (var lookupTable in lookupTableNames.OrderBy(t => t))
        {
            arrangeSteps.Add(new JObject
            {
                ["Strategy"] = "JsonFileSeed",
                ["Parameters"] = new JObject
                {
                    ["table"] = lookupTable,
                    ["dataFile"] = $"{lookupTable}_seed.json",
                    ["allowIdentityInsert"] = true
                }
            });
        }

        // Add the main hierarchical seed
        arrangeSteps.Add(new JObject
        {
            ["Strategy"] = "HierarchicalSeed",
            ["Parameters"] = new JObject
            {
                ["dataFile"] = hierarchicalSeedFileName
            }
        });

        var testCaseJson = new JObject
        {
            ["TestCaseName"] = $"Scaffolded Test for {rootTable}",
            ["Arrange"] = arrangeSteps,
            ["Actions"] = new JArray {
                new JObject {
                    ["Strategy"] = "TODO: Your Action Strategy",
                    ["Parameters"] = new JObject { ["Comment"] = "TODO: Define the action to perform on the seeded data." }
                }
            },
            ["Assert"] = new JArray {
                new JObject {
                    ["Strategy"] = "TODO: Your Assert Strategy",
                    ["Parameters"] = new JObject { ["Comment"] = "TODO: Define assertions to validate the outcome." }
                }
            }
        };

        var testCasePath = Path.Combine(outputPath, $"{rootTable}_scaffold.test.json");
        await File.WriteAllTextAsync(testCasePath, testCaseJson.ToString(Formatting.Indented));
        Console.WriteLine($" -> Generated test case skeleton: {testCasePath}");
    }

    private static async Task<Dictionary<string, JArray>> LoadLookupTemplatesAsync(DataMap dataMap, string templateDirectory)
    {
        var lookupTemplates = new Dictionary<string, JArray>();

        // Identify all unique lookup tables referenced in the data map
        var uniqueLookupTableNames = dataMap.Tables
            .SelectMany(t => t.Lookups)
            .Select(l => l.LookupTable)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Load each lookup table's template file
        foreach (var tableName in uniqueLookupTableNames)
        {
            var lookupTemplatePath = Path.Combine(templateDirectory, $"{tableName}_template.json");
            if (File.Exists(lookupTemplatePath))
            {
                var lookupTemplateContent = await File.ReadAllTextAsync(lookupTemplatePath);
                lookupTemplates[tableName] = JArray.Parse(lookupTemplateContent); // Lookup templates are saved as JArray
            }
            else
            {
                Console.WriteLine($"Warning: Lookup template file not found for '{tableName}' at '{lookupTemplatePath}'. Skipping scaffolding for this lookup table.");
            }
        }

        return lookupTemplates;
    }

    private static async Task<List<object>> GetRootKeysAsync(ScaffoldOptions opts, Type pkType)
    {
        var keyStrings = new List<string>();
        if (opts.Keys.Any())
        {
            keyStrings.AddRange(opts.Keys);
        }
        else if (!string.IsNullOrEmpty(opts.KeysFile))
        {
            keyStrings.AddRange(await File.ReadAllLinesAsync(opts.KeysFile));
        }

        return keyStrings
            .Select(keyStr => keyStr.Trim())
            .Where(keyStr => !string.IsNullOrEmpty(keyStr))
            .Select(keyStr => Convert.ChangeType(keyStr, pkType))
            .ToList();
    }
}
