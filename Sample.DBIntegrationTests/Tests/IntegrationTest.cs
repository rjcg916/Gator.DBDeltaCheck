using DBDeltaCheck.Core.Abstractions;
using DBDeltaCheck.Core.Abstractions.Factories;
using DBDeltaCheck.Core.Attributes;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json;
using Respawn;
using Sample.DBIntegrationTests.Fixtures;
using Xunit;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace DB.IntegrationTests.Tests;

public class IntegrationTest  : TestBed<DependencyInjectionFixture>
{

    private readonly ISetupStrategyFactory _setupFactory;
    private readonly IDatabaseOperations _dbRepository;
    private readonly IDurableFunctionClient _durableFunctionClient;
    private readonly IActionStrategyFactory _actionFactory;
    private readonly IComparisonStrategyFactory _comparisonFactory;
    private readonly ICleanupStrategyFactory _cleanupFactory;
    private readonly Respawner _respawner;

   public IntegrationTest(Xunit.ITestOutputHelper testOutputHelper, DependencyInjectionFixture fixture)
            : base(testOutputHelper, fixture)
    {
        // Resolve services from the fixture's ServiceProvider
        _setupFactory = _fixture.GetService<ISetupStrategyFactory>(testOutputHelper);
        _dbRepository = _fixture.GetService<IDatabaseOperations>(testOutputHelper);
        _durableFunctionClient = _fixture.GetService<IDurableFunctionClient>(testOutputHelper);
        _actionFactory = _fixture.GetService<IActionStrategyFactory>(testOutputHelper);
        _comparisonFactory = _fixture.GetService<IComparisonStrategyFactory>(testOutputHelper);
        _cleanupFactory = _fixture.GetService<ICleanupStrategyFactory>(testOutputHelper);

    }

    [Theory]
    [DatabaseStateTest("Path/To/Your/TestCases")]
    public async Task RunDatabaseStateTest(MasterTestDefinition testCase)
    {
        // =================================================================
        // ARRANGE: Execute all setup actions defined in the JSON 
        // =================================================================
        foreach (var setupInstruction in testCase.Arrange.Actions)
        {
            var strategy = _setupFactory.GetStrategy(setupInstruction.Type);
            await strategy.ExecuteAsync(_dbRepository.GetDbConnection(), setupInstruction.Config);
        }
        try
        {
            // Iterate through each assertion defined in the test case
            foreach (var assertion in testCase.Validate.ExpectedState)
            {
                // Get the "after" state of the table from the database
                var actualState = await _dbRepository.GetTableStateAsync<object>(assertion.Table);

                // Load the expected state from the specified JSON file
                var expectedStateJson = File.ReadAllText(Path.Combine("TestData", assertion.ExpectedDataFilePath));
                var expectedState = JsonConvert.DeserializeObject<List<object>>(expectedStateJson);

                // Use the factory to get the correct comparison strategy
                var comparisonStrategy = _comparisonFactory.GetStrategy(assertion.ComparisonStrategy.Name);

                // Execute the assertion using the chosen strategy
                comparisonStrategy.AssertState(actualState, expectedState, assertion.ComparisonStrategy.Options);
            }
            ;
        }
        catch { 
        
        }


        try
        {
            // =================================================================
            // ACT: Execute the primary action(s) of the test 
            // =================================================================
            foreach (var actionInstruction in testCase.Act.Actions)
            {
                // 3. Get the correct strategy from the factory based on the 'type' in JSON 
                var actionStrategy = _actionFactory.GetStrategy(actionInstruction.Type);

                // 4. Execute the strategy, passing its specific configuration
                await actionStrategy.ExecuteAsync(actionInstruction.Config);
            }

            // =================================================================
            // ASSERT: Verify the outcome is as expected
            // =================================================================

            // Iterate through each assertion defined in the test case
            foreach (var assertion in testCase.Assert.ExpectedState)
            {
                // Get the "after" state of the table from the database
                var actualState = await _dbRepository.GetTableStateAsync<object>(assertion.Table);

                // Load the expected state from the specified JSON file
                var expectedStateJson = File.ReadAllText(Path.Combine("TestData", assertion.ExpectedDataFilePath));
                var expectedState = JsonConvert.DeserializeObject<List<object>>(expectedStateJson);

                // Use the factory to get the correct comparison strategy
                var comparisonStrategy = _comparisonFactory.GetStrategy(assertion.ComparisonStrategy.Name);

                // Execute the assertion using the chosen strategy
                comparisonStrategy.AssertState(actualState, expectedState, assertion.ComparisonStrategy.Options);
            }
        }
        finally
        {
            // =================================================================
            // CLEANUP: Always execute teardown actions 
            // =================================================================
            if (testCase.Teardown?.Actions != null)
            {
                foreach (var cleanupInstruction in testCase.Teardown.Actions)
                {
                    var strategy = _cleanupFactory.GetStrategy(cleanupInstruction.Type);
                    await strategy.ExecuteAsync(_dbRepository, cleanupInstruction.Config);
                }
            }
        }
    }
}