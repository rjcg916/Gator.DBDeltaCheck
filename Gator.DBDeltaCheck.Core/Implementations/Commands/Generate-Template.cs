using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Commands;
public partial class CommandLineHandler
{

    [Verb("generate-template", HelpText = "Generates a blank, annotated template and a companion map file.")]
    public class GenerateTemplateOptions
    {
        [Option("root-table", Required = true, HelpText = "Name of the root database table.")]
        public string RootTable { get; set; }

        [Option("output-path", HelpText = "Directory for the output files. Defaults to the current directory.")]
        public string OutputPath { get; set; }
    }

    public async Task<int> RunTemplateGenerator(GenerateTemplateOptions opts)
    {
        var generator = _host.Services.GetRequiredService<HierarchyTemplateGenerator>();

        var outputPath = string.IsNullOrEmpty(opts.OutputPath) ? Directory.GetCurrentDirectory() : opts.OutputPath;

        Console.WriteLine($"Generating test kit for root table: '{opts.RootTable}'...");

        var (templateObject, dataMap) = generator.GenerateTestKit(opts.RootTable);

        var dataFileJson = new JObject
        {
            ["rootTable"] = opts.RootTable,
            ["data"] = new JArray(templateObject)
        };

        var outputDir = Path.Combine(outputPath, "GeneratedKits", opts.RootTable);
        Directory.CreateDirectory(outputDir);

        var dataOutputPath = Path.Combine(outputDir, $"{opts.RootTable}_template.json");
        await File.WriteAllTextAsync(dataOutputPath, dataFileJson.ToString(Formatting.Indented));

        var mapFileContent = JsonConvert.SerializeObject(dataMap, Formatting.Indented);
        var mapOutputPath = Path.Combine(outputDir, $"{opts.RootTable}_map.json");
        await File.WriteAllTextAsync(mapOutputPath, mapFileContent);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Successfully generated test kit in: {outputDir}");
        Console.ResetColor();
        return 0; // Success
    }
}
