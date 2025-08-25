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

        var pkType = await schemaService.GetPrimaryKeyTypeAsync(opts.RootTable);

        var rootKeys = await GetRootKeysAsync(opts, pkType);
        if (!rootKeys.Any())
        {
            await Console.Error.WriteLineAsync("ERROR: No valid keys were provided. One of --keys or --keys-file must be specified and contain values.");
            return 1;
        }

        var mapContent = await File.ReadAllTextAsync(opts.MapFile);
        var dataMap = JsonConvert.DeserializeObject<DataMap>(mapContent);

        Console.WriteLine($"Scaffolding {rootKeys.Count} records from root table '{opts.RootTable}'...");

        var results = await scaffolder.Scaffold(opts.RootTable, rootKeys, dataMap, templateData, opts.ExcludeDefaults);

        if (opts.OutputMode.Equals("Single", StringComparison.OrdinalIgnoreCase))
        {
            var finalJson = new JObject { ["rootTable"] = opts.RootTable, ["data"] = new JArray(results) };
            var singleFilePath = Path.Combine(outputPath, $"{opts.RootTable}_seed.json");
            await File.WriteAllTextAsync(singleFilePath, finalJson.ToString(Formatting.Indented));
        }
        else
        {
            Directory.CreateDirectory(outputPath);
            for (var i = 0; i < results.Count; i++)
            {
                // The seed file format requires the "data" property to be an array of objects.
                // Wrap the single result object in a JArray.
                var finalJson = new JObject { ["rootTable"] = opts.RootTable, ["data"] = new JArray(results[i]) };
                var safeKey = string.Join("_", rootKeys[i].ToString().Split(Path.GetInvalidFileNameChars()));
                var fileName = Path.Combine(outputPath, $"{opts.RootTable}_{safeKey}_seed.json");
                await File.WriteAllTextAsync(fileName, finalJson.ToString(Formatting.Indented));
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Successfully generated {results.Count} result(s) in '{outputPath}'.");
        Console.ResetColor();
        return 0; // Success
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
