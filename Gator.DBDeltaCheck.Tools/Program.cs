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

public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        var command = args[0].ToLowerInvariant();
        var commandArgs = args.Skip(1).ToArray();

        // Command router to select the correct tool
        switch (command)
        {
            case "scaffold":
                await RunScaffolder(commandArgs);
                break;

            case "generate-template":
                await RunTemplateGenerator(commandArgs);
                break;

            default:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Unknown command '{command}'.");
                Console.ResetColor();
                PrintUsage();
                break;
        }
    }

    /// <summary>
    /// Runs the Hierarchy Scaffolder to generate populated seed files from the database.
    /// </summary>
    private static async Task RunScaffolder(string[] args)
    {
        var arguments = ParseArguments(args);
        if (!arguments.ContainsKey("--template-file") || !arguments.ContainsKey("--map-file") ||
            (!arguments.ContainsKey("--keys") && !arguments.ContainsKey("--keys-file")))
        {
            PrintUsage();
            return;
        }

        var templateFilePath = arguments["--template-file"];
        var rootTableName = arguments["--root-table"];
        var mapFilePath = arguments["--map-file"];

        var templateContent = await File.ReadAllTextAsync(templateFilePath);
        var templateJson = JObject.Parse(templateContent);
        var templateData = (templateJson["data"] as JArray)?.FirstOrDefault() as JObject
                           ?? throw new InvalidOperationException("Template file must have a 'data' array with at least one object.");

        var outputMode = arguments.GetValueOrDefault("--output-mode", "Multiple");
        var outputPath = arguments.GetValueOrDefault("--output-path", Directory.GetCurrentDirectory());
        var excludeDefaults = arguments.ContainsKey("--exclude-defaults") || arguments.ContainsKey("-ed");

        var rootKeys = new List<object>();
        if (arguments.TryGetValue("--keys", out var keysString))
        {
            rootKeys.AddRange(keysString.Split(',').Select(k => k.Trim()));
        }
        else if (arguments.TryGetValue("--keys-file", out var keysFile))
        {
            rootKeys.AddRange(await File.ReadAllLinesAsync(keysFile));
        }

        var host = BuildHost();
        var scaffolder = host.Services.GetRequiredService<HierarchyScaffolder>();

        var mapContent = await File.ReadAllTextAsync(mapFilePath);
        var dataMap = JsonConvert.DeserializeObject<DataMap>(mapContent);

        Console.WriteLine($"Scaffolding {rootKeys.Count} records from root table '{rootTableName}'...");

        var results = await scaffolder.Scaffold(rootTableName, rootKeys, dataMap, templateData, excludeDefaults);

        if (outputMode.Equals("Single", StringComparison.OrdinalIgnoreCase))
        {
            var finalJson = new JObject { ["rootTable"] = rootTableName, ["data"] = new JArray(results) };
            await File.WriteAllTextAsync(outputPath, finalJson.ToString(Formatting.Indented));
        }
        else
        {
            Directory.CreateDirectory(outputPath);
            for (int i = 0; i < results.Count; i++)
            {
                var finalJson = new JObject { ["rootTable"] = rootTableName, ["data"] = results[i] };
                var fileName = Path.Combine(outputPath, $"{rootTableName}_{rootKeys[i]}_seed.json");
                await File.WriteAllTextAsync(fileName, finalJson.ToString(Formatting.Indented));
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Successfully generated {results.Count} file(s) in '{outputPath}'.");
        Console.ResetColor();
    }

    /// <summary>
    /// Runs the Hierarchy Template Generator to create blank, annotated templates and map files.
    /// </summary>
    private static async Task RunTemplateGenerator(string[] args)
    {
        var arguments = ParseArguments(args);
        if (!arguments.ContainsKey("--root-table"))
        {
            PrintUsage();
            return;
        }

        var rootTableName = arguments["--root-table"];
        var outputPath = arguments.GetValueOrDefault("--output-path", Directory.GetCurrentDirectory());

        var host = BuildHost();
        var generator = host.Services.GetRequiredService<HierarchyTemplateGenerator>();

        Console.WriteLine($"Generating test kit for root table: '{rootTableName}'...");

        var (templateObject, dataMap) = generator.GenerateTestKit(rootTableName);

        var dataFileJson = new JObject
        {
            ["rootTable"] = rootTableName,
            ["data"] = new JArray(templateObject)
        };
        var dataFileContent = dataFileJson.ToString(Formatting.Indented);

        var outputDir = Path.Combine(outputPath, "GeneratedKits", rootTableName);
        Directory.CreateDirectory(outputDir);

        var dataOutputPath = Path.Combine(outputDir, $"{rootTableName}_template.json");
        await File.WriteAllTextAsync(dataOutputPath, dataFileContent);

        var mapFileContent = JsonConvert.SerializeObject(dataMap, Formatting.Indented);
        var mapOutputPath = Path.Combine(outputDir, $"{rootTableName}_map.json");
        await File.WriteAllTextAsync(mapOutputPath, mapFileContent);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Successfully generated test kit in: {outputDir}");
        Console.ResetColor();
    }

    /// <summary>
    /// Configures and builds the dependency injection host.
    /// </summary>
    private static IHost BuildHost() =>
        Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                // Set the base path to the directory where the app's .exe is located.
                // This ensures it finds the appsettings.json file that was copied during the build.
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

                // Register the utility tools
                services.AddTransient<HierarchyScaffolder>();
                services.AddTransient<HierarchyTemplateGenerator>();
            })
            .Build();

    /// <summary>
    /// A simple parser for key-value command-line arguments.
    /// </summary>
    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    dict[args[i]] = args[i + 1];
                    i++; // Skip the value
                }
                else
                {
                    dict[args[i]] = "true"; // Handle boolean flags
                }
            }
        }
        return dict;
    }

    private static void PrintUsage()
    {
        // --- CHANGE: Updated help text ---
        Console.WriteLine("Gator.DBDeltaCheck Tools");
        Console.WriteLine("------------------------");
        Console.WriteLine("\nUsage: dotnet run --project Gator.DBDeltaCheck.Tools -- [command] [options]");
        Console.WriteLine("\nCommands:");
        Console.WriteLine("  scaffold           Generates a populated seed file from existing database data.");
        Console.WriteLine("                     Requires --template-file, --map-file, and --keys or --keys-file.");
        Console.WriteLine("  generate-template  Generates a blank, annotated hierarchical template and a companion map file.");
        Console.WriteLine("                     Requires --root-table.");
    }
}
