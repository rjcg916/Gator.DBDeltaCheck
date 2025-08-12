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
    private readonly IDataComparisonRuleFactory _comparisonFactory;
    private readonly IAssertionStrategyFactory _assertionFactory;   

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
        _comparisonFactory = _fixture.GetService<IDataComparisonRuleFactory>(testOutputHelper);
        _assertionFactory = _fixture.GetService<IAssertionStrategyFactory>(testOutputHelper);
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
        if (testCaseDir == null)
        {
            throw new InvalidOperationException("Test case definition file path is invalid.");
        }

        // if provided, read test/global data map
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
  
                // Add the base path to the parameters so the strategy can find relative files.
                setupInstruction.Parameters["_basePath"] = testCaseDir;

                var stepMap = globalDataMap;

                // If a local map override is defined in this step's parameters, load it.
                var localMapPath = setupInstruction.Parameters["dataMapFile"]?.Value<string>(); 
                if (!string.IsNullOrEmpty(localMapPath))
                {
                    stepMap = await LoadDataMapAsync(testCaseDir, localMapPath);
                }

                var strategy = _setupFactory.GetStrategy(setupInstruction.Strategy);
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

                // 1. Get the correct high-level strategy (e.g., "Flat" or "Hierarchical").
                var strategy = _assertionFactory.GetStrategy(assertion.Strategy);

                // 2. Execute the strategy, passing all necessary context.
                // The strategy itself now handles all the details of querying, mapping, and comparing.
                await strategy.AssertState(
                    assertion.Parameters,
                    testContext,
                    globalDataMap,
                    testCaseDir
                );
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
                    var strategy = _cleanupFactory.GetStrategy(cleanupInstruction.Strategy);
                    await strategy.ExecuteAsync(cleanupInstruction.Parameters);
                }
        }
    }

    /// <summary>
    /// A helper method to load a data map file if the path is provided.
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
    ///     Recursively scan a JToken for string values
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