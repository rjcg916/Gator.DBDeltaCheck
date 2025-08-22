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

    public async Task<int> RunScaffolder(ScaffoldOptions opts)
    {
        if (!opts.Keys.Any() && string.IsNullOrEmpty(opts.KeysFile))
        {
            Console.Error.WriteLine("ERROR: One of --keys or --keys-file must be provided.");
            return 1;
        }

        var outputPath = string.IsNullOrEmpty(opts.OutputPath) ? Directory.GetCurrentDirectory() : opts.OutputPath;

        var templateContent = await File.ReadAllTextAsync(opts.TemplateFile);
        var templateJson = JObject.Parse(templateContent);
        var templateData = (templateJson["data"] as JArray)?.FirstOrDefault() as JObject
                           ?? throw new InvalidOperationException("Template file must have a 'data' array with at least one object.");

        var scaffolder = _host.Services.GetRequiredService<HierarchyScaffolder>();
        var schemaService = _host.Services.GetRequiredService<IDbSchemaService>();

        var pkType = await schemaService.GetPrimaryKeyTypeAsync(opts.RootTable);

        var rootKeys = new List<object>();
        if (opts.Keys.Any())
        {
            foreach (var keyStr in opts.Keys)
            {
                rootKeys.Add(Convert.ChangeType(keyStr.Trim(), pkType));
            }
        }
        else if (!string.IsNullOrEmpty(opts.KeysFile))
        {
            foreach (var keyStr in await File.ReadAllLinesAsync(opts.KeysFile))
            {
                rootKeys.Add(Convert.ChangeType(keyStr.Trim(), pkType));
            }
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
                var finalJson = new JObject { ["rootTable"] = opts.RootTable, ["data"] = results[i] };
                var fileName = Path.Combine(outputPath, $"{opts.RootTable}_{rootKeys[i]}_seed.json");
                await File.WriteAllTextAsync(fileName, finalJson.ToString(Formatting.Indented));
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Successfully generated {results.Count} result(s) in '{outputPath}'.");
        Console.ResetColor();
        return 0; // Success
    }
}
