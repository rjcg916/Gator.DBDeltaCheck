using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json;
using System.Reflection;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Gator.DBDeltaCheck.Core.Attributes;

/// <summary>
///     A custom XUnit v3 DataAttribute that discovers and loads test cases from .test.json files.
///     It provides a MasterTestDefinition object as a parameter to the test method for each file found.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class DatabaseStateTestAttribute(string path) : DataAttribute
{
    /// <summary>
    ///     Overrides the abstract method from DataAttribute to provide test data.
    ///     This is the main entry point for XUnit to get the theory data.
    /// </summary>
    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(
        MethodInfo testMethod,
        DisposalTracker disposalTracker)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("A path to the test case directory must be provided.", nameof(path));

        var absolutePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));

        if (!Directory.Exists(absolutePath))
            throw new DirectoryNotFoundException($"Could not find the test case directory: {absolutePath}");

        var testDefinitions = new List<ITheoryDataRow>();
        var files = Directory.GetFiles(absolutePath, "*.test.json", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var testDefinition = LoadTestDefinition(file);

            // Create a TheoryDataRow, which is the object XUnit v3 expects.
            var dataRow = new TheoryDataRow<MasterTestDefinition>(testDefinition)
            {
                // Set the custom display name using the TestCaseName from the JSON file.
                // This name will appear in the Test Explorer.
                TestDisplayName = testDefinition.TestCaseName
            };

            testDefinitions.Add(dataRow);
        }

        // Wrap the result in a ValueTask for the async-first API.
        return new ValueTask<IReadOnlyCollection<ITheoryDataRow>>(testDefinitions);
    }

    /// <summary>
    ///     Overrides the abstract method to indicate that the test runner can enumerate
    ///     our test cases during the discovery phase. This allows individual test cases
    ///     (one per file) to appear in the Test Explorer.
    /// </summary>
    public override bool SupportsDiscoveryEnumeration()
    {
        return true;
    }

    private MasterTestDefinition LoadTestDefinition(string filePath)
    {
        var fileContent = File.ReadAllText(filePath);
        var testDefinition = JsonConvert.DeserializeObject<MasterTestDefinition>(fileContent);

        if (testDefinition == null)
            throw new InvalidOperationException($"Failed to deserialize test definition from {filePath}.");

        testDefinition.DefinitionFilePath = filePath;
        testDefinition.TestCaseName ??= Path.GetFileNameWithoutExtension(filePath);

        return testDefinition;
    }
}