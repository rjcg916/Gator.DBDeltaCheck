using System.Reflection;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Gator.DBDeltaCheck.Core.Attributes;

/// <summary>
///     A custom XUnit v3 DataAttribute that discovers and loads test cases from .test.json files.
///     It provides a MasterTestDefinition object as a parameter to the test method for each file found.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class DatabaseStateTestAttribute : DataAttribute
{
    private readonly string _path;

    public DatabaseStateTestAttribute(string path)
    {
        _path = path;
    }

    /// <summary>
    ///     Overrides the abstract method from DataAttribute to provide test data.
    ///     This is the main entry point for XUnit to get the theory data.
    /// </summary>
    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(
        MethodInfo testMethod,
        DisposalTracker disposalTracker)
    {
        if (string.IsNullOrEmpty(_path))
            throw new ArgumentException("A path to the test case directory must be provided.", nameof(_path));

        var absolutePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), _path));

        if (!Directory.Exists(absolutePath))
            throw new DirectoryNotFoundException($"Could not find the test case directory: {absolutePath}");

        var testDefinitions = new List<ITheoryDataRow>();
        var files = Directory.GetFiles(absolutePath, "*.test.json", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var testDefinition = LoadTestDefinition(file);

            // Use the base class's helper method to convert our object[] into an ITheoryDataRow.
            // This is the correct pattern for v3.
            testDefinitions.Add(ConvertDataRow(new object[] { testDefinition }));
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