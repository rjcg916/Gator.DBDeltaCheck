# Gator.DBDeltaCheck: A Data-Driven Database Integration Testing Framework

Gator.DBDeltaCheck is a powerful and extensible .NET framework designed to simplify database integration testing. It allows you to define complex test scenarios—including database setup, actions, and state validation—entirely through simple JSON configuration files, keeping your test logic clean and separate from your test data.

This framework is built on a "strategy" pattern, allowing you to easily extend it with new actions (e.g., calling different APIs), new data seeding methods, and new ways to compare results, without ever touching the core test runner.

## Core Concepts

- **Data-Driven by Default:** Every test is defined in a `.test.json` file. This file orchestrates the entire test flow, from arranging the initial database state to asserting the final outcome.
- **Arrange, Act, Assert, Cleanup:** The framework follows the classic testing pattern. Each phase is controlled by your JSON configuration.
- **Extensible Strategies:** The real power of the framework lies in its pluggable architecture.
  - **`ISetupStrategy`**: Defines how to arrange the database state (e.g., `HierarchicalSeed`, `JsonFileSeed`).
  - **`IActionStrategy`**: Defines the primary action of the test (e.g., `DurableFunctionAction`, `ApiCallAction`).
  - **`IComparisonStrategy`**: Defines how to validate the results (e.g., `StrictEquivalence`, `IgnoreOrder`, `IgnoreColumns`).
  - **`ICleanupStrategy`**: Defines how to clean up the database after a test (e.g., using `Respawn`).
- **Schema-Aware Seeding:** The `HierarchicalSeedingStrategy` uses an `IDbSchemaService` (powered by EF Core) to understand your database's relationships, automatically resolving foreign keys and allowing you to seed complex, nested data with ease.

---

## How to Use This Framework

This repository is structured into four projects to demonstrate how to use the framework effectively.

1.  **`Gator.DBDeltaCheck.Core`**: The core framework library. You would typically package this as a NuGet package and reference it in your test projects.
2.  **`Sample.DBIntegrationTests`**: The actual test project that consumes the core framework to test the `ECommerceDemo` database.
3.  **`ECommerceDemo.Api`**: A sample API for data access to underlying database operations. This API is used in the tests to perform actions like creating orders.
4.  **`ECommerceDemo.Data`**: A sample EF Core project representing the database you want to test.

### Step 1: Set Up Your Test Project's Dependencies

In your test project (`Sample.DBIntegrationTests`), you need to configure the dependency injection container to know about your database, your strategies, and the framework's services. This is done in the `DependencyInjectionFixture.cs` file.

```csharp
// In DependencyInjectionFixture.cs

// 1. Register your application's DbContext
services.AddDbContext<ECommerceDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Register the framework's core services
services.AddSingleton<IDbSchemaService, EFCachingDbSchemaService>();
services.AddSingleton<IDatabaseRepository>(new DapperDatabaseRepository(connectionString));

// 3. Register all the factories
services.AddSingleton<ISetupStrategyFactory, SetupStrategyFactory>();
// ... other factories ...

// 4. Register all the available strategy implementations
services.AddTransient<ISetupStrategy, HierarchicalSeedingStrategy>();
services.AddTransient<ISetupStrategy, JsonFileSeedingStrategy>();
services.AddTransient<IActionStrategy, ApiCallActionStrategy>();
// ... other strategies ...
```

### Step 2: Create a Test Definition File

This is where you define your test case. Create a folder structure for your tests, for example: `TestCases/Orders/CreateOrder.test.json`.

This JSON file tells the framework exactly what to do.

**`CreateOrder.test.json` Example:**

```json
{
  "TestCaseName": "Happy Path: Successfully create a new Order",
  "Arrange": [
    {
      "Strategy": "HierarchicalSeed",
      "Parameters": {
        "rootTable": "Customers",
        "data": [
          {
            "FirstName": "John",
            "LastName": "Doe",
            "Email": "john.doe@example.com"
          }
        ]
      }
    }
  ],
  "Act": {
    "Strategy": "ApiCall",
    "Parameters": {
      "Endpoint": "/api/orders",
      "Method": "POST",
      "Payload": {
        "CustomerId": 1,
        "OrderDate": "2025-07-23T10:00:00Z",
        "OrderItems": [
          {
            "ProductId": 101,
            "Quantity": 2
          }
        ]
      }
    }
  },
  "Assert": [
    {
      "TableName": "Orders",
      "ExpectedDataFile": "expected/orders_expected.json",
      "ComparisonStrategy": "IgnoreColumns",
      "ComparisonParameters": [ "OrderId", "TotalAmount" ]
    },
    {
      "TableName": "OrderItems",
      "ExpectedDataFile": "expected/order_items_expected.json"
    }
  ]
}
```

### Step 3: Create Supporting Data Files

Your test definition file will reference other files, like the expected results. Create these files in the same directory.

**`expected/orders_expected.json` Example:**

```json
[
  {
    "CustomerId": 1,
    "OrderStatusId": 1
  }
]
```

### Step 4: Write the Test Runner Class

The C# test class is incredibly simple. Its only job is to point to the directory containing your test cases. The `[TestDefinitionData]` attribute and the framework handle the rest.

```csharp
// In IntegrationTest.cs

public class IntegrationTest : TestBed<DependencyInjectionFixture>
{
    // The constructor resolves all the factories and services from the DI container.
    public IntegrationTest(ITestOutputHelper testOutputHelper, DependencyInjectionFixture fixture)
        : base(testOutputHelper, fixture)
    {
        // ... resolve factories and services ...
    }

    [Theory]
    // This attribute tells the test runner to look for all *.test.json files
    // in the "TestCases" directory and run this method for each one.
    [TestDefinitionData("TestCases")]
    public async Task RunDatabaseStateTest(MasterTestDefinition testCase)
    {
        // The test runner logic inside this method will:
        // 1. Use Respawner to clean the database.
        // 2. Use the ISetupStrategyFactory to execute the "Arrange" steps.
        // 3. Use the IActionStrategyFactory to execute the "Act" step.
        // 4. Use the IComparisonStrategyFactory to execute the "Assert" steps.
        // 5. Use the ICleanupStrategyFactory to execute any final cleanup.
    }
}
```

By following this pattern, you can build a comprehensive suite of robust, maintainable, and easy-to-read database integration tests.
