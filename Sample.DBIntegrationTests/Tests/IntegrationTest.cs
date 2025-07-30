using FluentAssertions;
using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Gator.DBDeltaCheck.Core.Attributes;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json;
using Respawn;
using Sample.DBIntegrationTests.Fixtures;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace DB.IntegrationTests.Tests;

public class IntegrationTest : TestBed<DependencyInjectionFixture>
{
    // Factories for creating strategies dynamically
    private readonly ISetupStrategyFactory _setupFactory;
    private readonly IActionStrategyFactory _actionFactory;
    private readonly IComparisonStrategyFactory _comparisonFactory;
    private readonly ICleanupStrategyFactory _cleanupFactory;

    // Core services needed for test execution
    private readonly IDatabaseRepository _dbRepository;
    private readonly Task<Respawner> _respawnerTask; 

    public IntegrationTest(Xunit.ITestOutputHelper testOutputHelper, DependencyInjectionFixture fixture)
             : base(testOutputHelper, fixture)
    {
        // Resolve services from the fixture's ServiceProvider
        _setupFactory = _fixture.GetService<ISetupStrategyFactory>(testOutputHelper);
        _actionFactory = _fixture.GetService<IActionStrategyFactory>(testOutputHelper);
        _comparisonFactory = _fixture.GetService<IComparisonStrategyFactory>(testOutputHelper);
        _cleanupFactory = _fixture.GetService<ICleanupStrategyFactory>(testOutputHelper);

        _dbRepository = _fixture.GetService<IDatabaseRepository>(testOutputHelper);
        _respawnerTask = _fixture.GetService<Task<Respawner>>(testOutputHelper);
    }

    [Theory]
    [DatabaseStateTest("TestCases")]
    public async Task RunDatabaseStateTest(MasterTestDefinition testCase)
    {
        // A robust try/finally block ensures that cleanup ALWAYS runs,
        // even if the test fails during the Act or Assert phase.
        try
        {
            // =================================================================
            // INITIAL CLEANUP: Reset the database to a pristine state.
            // =================================================================
            var respawner = await _respawnerTask; 
            await using var connection = _dbRepository.GetDbConnection();
            await connection.OpenAsync();
            await respawner.ResetAsync(connection);

            // =================================================================
            // ARRANGE: Execute all setup actions defined in the JSON.
            // =================================================================

            foreach (var setupInstruction in testCase.Arrangements)
            {
                var strategy = _setupFactory.Create(setupInstruction.Strategy);
      
                // Add the base path to the parameters so the strategy can find relative files.
                setupInstruction.Parameters["_basePath"] = Path.GetDirectoryName(testCase.DefinitionFilePath);

                await strategy.ExecuteAsync(setupInstruction.Parameters);

            }

            // =================================================================
            // ACT: Execute the primary action(s) of the test.
            // =================================================================
            foreach (var actInstruction in testCase.Actions)
            {
                var actionStrategy = _actionFactory.GetStrategy(actInstruction.Strategy);
                await actionStrategy.ExecuteAsync(actInstruction.Parameters);
            }


            // =================================================================
            // ASSERT: Verify the outcome is as expected.
            // =================================================================
            foreach (var assertion in testCase.Assertions)
            {
                // CHANGE: The assertion logic now aligns with our IComparisonStrategy design.
                // 1. Get the actual state of the table AFTER the action.
                var actualData = await _dbRepository.QueryAsync<object>(
                    $"SELECT * FROM {assertion.Table}"
                );
                var actualStateJson = JsonConvert.SerializeObject(actualData);

                // 2. Load the expected state from the specified JSON file.
                // The path is resolved relative to the test definition file itself.
                var testCaseDir = Path.GetDirectoryName(testCase.DefinitionFilePath);
                var expectedDataPath = Path.Combine(testCaseDir, assertion.ExpectedDataFile);
                var expectedStateJson = await File.ReadAllTextAsync(expectedDataPath);

                // 3. Use the factory to get the correct comparison strategy.
                var comparisonStrategy = _comparisonFactory.GetStrategy(assertion.ComparisonStrategy.Strategy);

                // 4. Execute the comparison.
                var areEqual = comparisonStrategy.Compare(
                    null, // beforeStateJson - not used in this simple assertion
                    actualStateJson,
                    expectedStateJson,
                    assertion.ComparisonStrategy.Parameters
                );

                // 5. Use FluentAssertions for a clear pass/fail message.
                areEqual.Should().BeTrue(
                    $"comparison failed for table '{assertion.Table}' using strategy '{assertion.ComparisonStrategy}'."
                );
            }
        }
        finally
        {
            // =================================================================
            // FINAL CLEANUP: Optionally run specific teardown actions.
            // =================================================================
            if (testCase.Teardowns != null)
            {
                foreach (var cleanupInstruction in testCase.Teardowns)
                {
                    var strategy = _cleanupFactory.Create(cleanupInstruction.Strategy);
                    await strategy.ExecuteAsync(cleanupInstruction.Parameters);
                }
            }
        }
    }
}