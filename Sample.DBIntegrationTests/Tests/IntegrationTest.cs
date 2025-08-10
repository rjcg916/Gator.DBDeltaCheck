using FluentAssertions;
using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Gator.DBDeltaCheck.Core.Attributes;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Respawn;
using Sample.DBIntegrationTests.Fixtures;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace Sample.DBIntegrationTests.Tests;

public class IntegrationTest : TestBed<DependencyInjectionFixture>
{
    private readonly IActionStrategyFactory _actionFactory;
    private readonly ICleanupStrategyFactory _cleanupFactory;
    private readonly IComparisonStrategyFactory _comparisonFactory;

    // Core services needed for test execution
    private readonly IDatabaseRepository _dbRepository;

    private readonly Task<Respawner> _respawnerTask;

    // Factories for creating strategies dynamically
    private readonly ISetupStrategyFactory _setupFactory;


    private readonly IDataMapper _dataMapper;

    public IntegrationTest(ITestOutputHelper testOutputHelper, DependencyInjectionFixture fixture)
        : base(testOutputHelper, fixture)
    {
        // Resolve services from the fixture's ServiceProvider
        _setupFactory = _fixture.GetService<ISetupStrategyFactory>(testOutputHelper);
        _actionFactory = _fixture.GetService<IActionStrategyFactory>(testOutputHelper);
        _comparisonFactory = _fixture.GetService<IComparisonStrategyFactory>(testOutputHelper);
        _cleanupFactory = _fixture.GetService<ICleanupStrategyFactory>(testOutputHelper);

        _dbRepository = _fixture.GetService<IDatabaseRepository>(testOutputHelper);
        _respawnerTask = _fixture.GetService<Task<Respawner>>(testOutputHelper);

        _dataMapper = _fixture.GetService<IDataMapper>(testOutputHelper);
    }

    [Theory]
    [DatabaseStateTest("TestCases")]
    public async Task RunDatabaseStateTest(MasterTestDefinition testCase)
    {

        var testContext = new Dictionary<string, object>();
        var testCaseDir = Path.GetDirectoryName(testCase.DefinitionFilePath);

        DataMap? globalDataMap = null;
        if (!string.IsNullOrEmpty(testCase.DataMapFile))
        {
            var globalPath = Path.Combine(testCaseDir, testCase.DataMapFile);
            var mapContent = await File.ReadAllTextAsync(globalPath);
            globalDataMap = JsonConvert.DeserializeObject<DataMap>(mapContent);
        }

        // A robust try/finally block ensures that cleanup ALWAYS runs,
        // even if the test fails during the Act or Assert phase.
        try
        {
            // =================================================================
            // INITIAL CLEANUP: Reset the database to a pristine state.
            // =================================================================
            var respawner = await _respawnerTask;
            await using var connection = _dbRepository.GetDbConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await respawner.ResetAsync(connection);

            // =================================================================
            // ARRANGE: Execute all setup actions defined in the JSON.
            // =================================================================
            foreach (var setupInstruction in testCase.Arrange)
            {
                var strategy = _setupFactory.Create(setupInstruction.Strategy);

                // Add the base path to the parameters so the strategy can find relative files.
                setupInstruction.Parameters["_basePath"] = testCaseDir;

                var stepMap = globalDataMap;
                var localMapPath = setupInstruction.Parameters["dataMapFile"]?.Value<string>();

                // If a local override is defined in this step's parameters, load it.
                if (!string.IsNullOrEmpty(localMapPath))
                {
                    stepMap = await LoadDataMapAsync(testCaseDir, localMapPath);
                }

                await strategy.ExecuteAsync(setupInstruction.Parameters, testContext, stepMap);
            }

            // =================================================================
            // ACT: Execute the primary action(s) of the test.
            // =================================================================
            foreach (var actInstruction in testCase.Actions)
            {
                ResolveTokens(actInstruction.Parameters, testContext);

                var actionStrategy = _actionFactory.GetStrategy(actInstruction.Strategy);
                await actionStrategy.ExecuteAsync(actInstruction.Parameters);
            }


            // =================================================================
            // ASSERT: Verify the outcome is as expected.
            // =================================================================
            foreach (var assertion in testCase.Assert)
            {

                // 1. Load the expected state from the specified JSON file.

                var expectedDataPath = Path.Combine(testCaseDir, assertion.ExpectedDataFile);
                var expectedStateJson = await File.ReadAllTextAsync(expectedDataPath, TestContext.Current.CancellationToken);

                // 2. Get the actual state of the table AFTER the action.
                var actualData = await _dbRepository.QueryAsync<object>(
                    $"SELECT * FROM {assertion.TableName}"
                );

                var rawAfterStateJson = JsonConvert.SerializeObject(actualData, Formatting.Indented);

                // 3. If a data map is provided, apply it to the actual state.
                string afterStateJson = rawAfterStateJson;

                var assertMap = await LoadDataMapAsync(testCaseDir, assertion.DataMapFile) ?? globalDataMap;

                if (assertMap != null)
                {
                    afterStateJson =
                        await _dataMapper.MapToFriendlyState(rawAfterStateJson, assertMap, assertion.TableName);
                }

                // 4. Use the factory to get the correct comparison strategy.
                var comparisonStrategy = _comparisonFactory.GetStrategy(assertion.ComparisonStrategy);

                // 5. Execute the comparison using the final, potentially resolved, expected state.
                var areEqual = comparisonStrategy.Compare(
                    null, // beforeStateJson - not used in this simple assertion
                    afterStateJson,
                    expectedStateJson,
                    assertion.ComparisonParameters
                );

                // 6. Use FluentAssertions for a clear pass/fail message.
                areEqual.Should().BeTrue(
                    $"comparison failed for table '{assertion.TableName}' using strategy '{assertion.ComparisonStrategy}' [ Actual '{afterStateJson}'<> Expected '{expectedStateJson}']");

            }
        }
        finally
        {
            // =================================================================
            // FINAL CLEANUP: Optionally run specific teardown actions.
            // =================================================================
            if (testCase.Teardown != null)
                foreach (var cleanupInstruction in testCase.Teardown)
                {
                    var strategy = _cleanupFactory.Create(cleanupInstruction.Strategy);
                    await strategy.ExecuteAsync(cleanupInstruction.Parameters);
                }
        }
    }

    /// <summary>
    /// A single, DRY helper method to load a data map file if the path is provided.
    /// </summary>
    private async Task<DataMap?> LoadDataMapAsync(string basePath, string? relativeMapPath)
    {
        if (string.IsNullOrEmpty(relativeMapPath))
        {
            return null;
        }

        var fullPath = Path.Combine(basePath, relativeMapPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"The data map file was not found: {fullPath}");
        }

        var mapContent = await File.ReadAllTextAsync(fullPath);
        return JsonConvert.DeserializeObject<DataMap>(mapContent);
    }

    /// <summary>
    ///     A helper method to recursively scan a JToken for string values
    ///     that match the token format "{key}" and replace them with values
    ///     from the test context.
    /// </summary>
    private void ResolveTokens(JToken token, Dictionary<string, object> context)
    {
        if (token is JObject obj)
        {
            foreach (var property in obj.Properties()) ResolveTokens(property.Value, context);
        }
        else if (token is JArray arr)
        {
            foreach (var item in arr) ResolveTokens(item, context);
        }
        else if (token is JValue val && val.Type == JTokenType.String)
        {
            var stringValue = val.Value<string>();
            if (stringValue.StartsWith("{") && stringValue.EndsWith("}"))
            {
                var key = stringValue.Trim('{', '}');
                if (context.TryGetValue(key, out var resolvedValue))
                    // Replace the token JValue with a new JValue of the correct type.
                    val.Replace(JToken.FromObject(resolvedValue));
            }
        }
    }
}