using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json;
using System.Reflection;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Gator.DBDeltaCheck.Core.Attributes;

public class DatabaseStateTestAttribute : DataAttribute
{
    private readonly string _filePath;

    // Constructor accepts a path to a single file or a directory.
    public DatabaseStateTestAttribute(string filePath)
    {
        _filePath = filePath;
    }


    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(_filePath));
        }

        var absolutePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _filePath));
        var testDefinitions = new List<ITheoryDataRow>();

        if (File.GetAttributes(absolutePath).HasFlag(FileAttributes.Directory))
        {
            var files = Directory.GetFiles(absolutePath, "*.test.json", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var testDef = LoadTestDefinition(file);
                testDefinitions.Add(ConvertDataRow(new object[] { testDef }));
            }
        }
        else
        {
            var testDef = LoadTestDefinition(absolutePath);
            testDefinitions.Add(ConvertDataRow(new object[] { testDef }));
        }

        return new ValueTask<IReadOnlyCollection<ITheoryDataRow>>(testDefinitions);
    }



    public override bool SupportsDiscoveryEnumeration()
    {
        throw new NotImplementedException();
    }

    private MasterTestDefinition LoadTestDefinition(string filePath)
    {
        var fileContent = File.ReadAllText(filePath);
        var testDefinition = JsonConvert.DeserializeObject<MasterTestDefinition>(fileContent);
        if (testDefinition == null)
        {
            throw new InvalidOperationException($"Failed to deserialize test definition from {filePath}.");
        }
        // Use the file name as the test name if not provided in the JSON
        testDefinition.TestName ??= Path.GetFileNameWithoutExtension(filePath);
        return testDefinition;
    }
}
