using CommandLine;
using EcommerceDemo.Data.Data;
using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Implementations;
using Gator.DBDeltaCheck.Core.Implementations.Mapping;
using Gator.DBDeltaCheck.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Tools;

#region Options Classes

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


[Verb("generate-template", HelpText = "Generates a blank, annotated template and a companion map file.")]
public class GenerateTemplateOptions
{
    [Option("root-table", Required = true, HelpText = "Name of the root database table.")]
    public string RootTable { get; set; }

    [Option("output-path", HelpText = "Directory for the output files. Defaults to the current directory.")]
    public string OutputPath { get; set; }
}
#endregion

public static class Program
{
    public static async Task Main(string[] args)
    {
        // The parser handles routing to the correct method based on the verb.
        var parser = new Parser(with => with.HelpWriter = Console.Error);
        await parser.ParseArguments<ScaffoldOptions, GenerateTemplateOptions>(args)
            .MapResult(
                (ScaffoldOptions opts) => RunScaffolder(opts),
                (GenerateTemplateOptions opts) => RunTemplateGenerator(opts),
                errs => Task.FromResult(1)); 
    }

    /// <summary>
    /// Runs the Hierarchy Scaffolder. 
    /// </summary>
    private static async Task<int> RunScaffolder(ScaffoldOptions opts)
    {
        if (!opts.Keys.Any() && string.IsNullOrEmpty(opts.KeysFile))
        {
            Console.Error.WriteLine("ERROR: One of --keys or --keys-file must be provided.");
            return 1; // Return an error code
        }

        var outputPath = string.IsNullOrEmpty(opts.OutputPath) ? Directory.GetCurrentDirectory() : opts.OutputPath;

        var templateContent = await File.ReadAllTextAsync(opts.TemplateFile);
        var templateJson = JObject.Parse(templateContent);
        var templateData = (templateJson["data"] as JArray)?.FirstOrDefault() as JObject
                           ?? throw new InvalidOperationException("Template file must have a 'data' array with at least one object.");

        var rootKeys = new List<object>();
        if (opts.Keys.Any())
        {
            rootKeys.AddRange(opts.Keys);
        }
        else if (!string.IsNullOrEmpty(opts.KeysFile))
        {
            rootKeys.AddRange(await File.ReadAllLinesAsync(opts.KeysFile));
        }

        var host = BuildHost();
        var scaffolder = host.Services.GetRequiredService<HierarchyScaffolder>();

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

    /// <summary>
    /// Runs the Hierarchy Template Generator.
    /// </summary>
    private static async Task<int> RunTemplateGenerator(GenerateTemplateOptions opts)
    {
        var outputPath = string.IsNullOrEmpty(opts.OutputPath) ? Directory.GetCurrentDirectory() : opts.OutputPath;

        var host = BuildHost();
        var generator = host.Services.GetRequiredService<HierarchyTemplateGenerator>();

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

    /// <summary>
    /// Configures and builds the dependency injection host. 
    /// </summary>
    private static IHost BuildHost() =>
        Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
                services.AddDbContext<ECommerceDbContext>(options => options.UseSqlServer(connectionString));
                services.AddScoped<DbContext>(sp => sp.GetRequiredService<ECommerceDbContext>());
                services.AddTransient<IDatabaseRepository, DapperDatabaseRepository>(sp => new DapperDatabaseRepository(connectionString));
                services.AddSingleton<IDbSchemaService, EFCachingDbSchemaService>();
                services.AddTransient<IDataMapperValueResolver, DataMapperValueResolver>();
                services.AddTransient<ResolveToDbStrategy>();
                services.AddTransient<MapToFriendlyStrategy>();
                services.AddTransient<IDataMapper, DataMapper>();
                services.AddTransient<HierarchyScaffolder>();
                services.AddTransient<HierarchyTemplateGenerator>();
            })
            .Build();
}