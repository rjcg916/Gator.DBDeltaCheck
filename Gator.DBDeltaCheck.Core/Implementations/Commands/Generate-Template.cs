using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

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

    private async Task<int> RunTemplateGenerator(GenerateTemplateOptions opts)
    {
        var generator = host.Services.GetRequiredService<HierarchyTemplateGenerator>();

        var outputPath = string.IsNullOrEmpty(opts.OutputPath) ? Directory.GetCurrentDirectory() : opts.OutputPath;

        Console.WriteLine($"Generating test kit for root table: '{opts.RootTable}'...");

        // Deconstruct the results
        var (templateObject, dataMap, lookupTemplates) = generator.GenerateTestKit(opts.RootTable);

        // The main template is wrapped in a structure expected by the HierarchicalSeed strategy.
        var dataFileJson = new JObject
        {
            ["rootTable"] = opts.RootTable,
            ["data"] = new JArray(templateObject)
        };

        // Create a dedicated directory for the generated kit.
        var outputDir = Path.Combine(outputPath, "GeneratedKits", opts.RootTable);
        Directory.CreateDirectory(outputDir);

        // Save the main hierarchical template file.
        var dataOutputPath = Path.Combine(outputDir, $"{opts.RootTable}_template.json");
        await File.WriteAllTextAsync(dataOutputPath, dataFileJson.ToString(Formatting.Indented));
        Console.WriteLine($" -> Saved main template: {dataOutputPath}");

        // Save the data map file.
        var mapFileContent = JsonConvert.SerializeObject(dataMap, Formatting.Indented);
        var mapOutputPath = Path.Combine(outputDir, $"{opts.RootTable}_map.json");
        await File.WriteAllTextAsync(mapOutputPath, mapFileContent);
        Console.WriteLine($" -> Saved data map: {mapOutputPath}");

        // Save the individual lookup table templates.
        if (lookupTemplates.Any())
        {
            Console.WriteLine("Generating lookup table templates...");
            foreach (var (tableName, lookupTemplate) in lookupTemplates)
            {
                var lookupTemplatePath = Path.Combine(outputDir, $"{tableName}_template.json");
                await File.WriteAllTextAsync(lookupTemplatePath, lookupTemplate.ToString(Formatting.Indented));
                Console.WriteLine($" -> Saved lookup template: {lookupTemplatePath}");
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Successfully generated test kit in: {outputDir}");
        Console.ResetColor();
        return 0; // Success
    }
}
