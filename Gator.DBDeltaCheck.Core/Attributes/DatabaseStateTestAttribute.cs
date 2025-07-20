using DBDeltaCheck.Core.Models;
using Newtonsoft.Json;
using System.Reflection;
using Xunit.Sdk;

namespace DBDeltaCheck.Core.Attributes;

public class DatabaseStateTestAttribute : DataAttribute
{
    private readonly string _filePath;

    // Constructor accepts a path to a single file or a directory.
    public DatabaseStateTestAttribute(string filePath)
    {
        _filePath = filePath;
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(_filePath));
        }

        var absolutePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _filePath));

        if (File.GetAttributes(absolutePath).HasFlag(FileAttributes.Directory))
        {
            // If it's a directory, enumerate all.test.json files
            var files = Directory.GetFiles(absolutePath, "*.test.json", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                yield return new object[] { LoadTestDefinition(file) };
            }
        }
        else
        {
            // If it's a single file
            yield return new object[] { LoadTestDefinition(absolutePath) };
        }
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
