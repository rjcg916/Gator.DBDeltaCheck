# Gator.DBDeltaCheck: A Data-Driven Database Integration Testing Framework

Gator.DBDeltaCheck is a powerful and extensible .NET framework designed to simplify database integration testing. It allows you to define complex test scenarios—including database setup, actions, and state validation—entirely through simple JSON configuration files, keeping your test logic clean and separate from your test data.

This framework is built on a "strategy" pattern, allowing you to easily extend it with new actions (e.g., calling APIs or Durable Functions), new data seeding methods, and new ways to compare results, without ever touching the core test runner.

## Core Concepts

-   **Data-Driven by Default:** Every test is defined in a `.test.json` file. This file orchestrates the entire test flow, from arranging the initial database state to asserting the final outcome.
-   **Arrange, Act, Assert, Cleanup:** The framework follows the classic testing pattern. Each phase is controlled by your JSON configuration.
-   **Extensible Strategies:** The real power of the framework lies in its pluggable architecture.
    -   **`ISetupStrategy`**: Defines how to arrange the database state (e.g., `HierarchicalSeed`, `JsonFileSeed`).
    -   **`IActionStrategy`**: Defines the primary action of the test (e.g., `ApiCallAction`, `DurableFunctionAction`).
    -   **`IComparisonStrategy`**: Defines how to validate the results (e.g., `HierarchicalCompare`, `IgnoreColumns`).
-   **Data Maps for Clean Tests:** Instead of cluttering your test data with database details, you can create a reusable "Data Map" file. This file tells the framework how to resolve foreign keys, allowing your seed and assert files to use clean, human-readable values (like `"OrderStatus": "Processing"`) instead of fragile database IDs.
-   **State Passing:** The framework allows you to capture values generated during the `Arrange` step (like a new database ID) and reuse them in later steps. This is essential for testing end-to-end flows.
-   **Hierarchical Assertions:** You are not limited to asserting against multiple flat tables. The **`HierarchicalCompare`** strategy allows you to query a full, nested business object from the database and compare it against a single, clean, hierarchical expected result file.

---

## How to Use This Framework

This repository is structured into four projects to demonstrate a real-world use case:

1.  **`Gator.DBDeltaCheck.Core`**: The core framework library.
2.  **`ECommerceDemo.Data`**: A sample EF Core project representing the database under test.
3.  **`ECommerceDemo.API`**: A sample Minimal API that performs actions against the database.
4.  **`Sample.DBIntegrationTests`**: The test project that uses the framework to run tests against the API and database.

### Step 1: Create a Reusable Data Map

A Data Map defines how to translate between human-readable values and database foreign keys. This file can be used by many tests.

**`maps/ecommerce.json`:**
```json
{
  "tables": [
    {
      "name": "Orders",
      "lookups": [
        { "dataProperty": "OrderStatus", "lookupTable": "OrderStatuses", "lookupValueColumn": "StatusName" },
        { "dataProperty": "Customer", "lookupTable": "Customers", "lookupValueColumn": "Email" }
      ]
    },
    {
      "name": "OrderItems",
      "lookups": [
        { "dataProperty": "Product", "lookupTable": "Products", "lookupValueColumn": "Name" }
      ]
    }
  ]
}
```

### Step 2: Create a Test Definition File

This file orchestrates a complete end-to-end test. This example will:
1.  Seed a new customer and some products.
2.  Capture the new `CustomerId`.
3.  Call an API to create an order for that new customer.
4.  Assert that the final, nested order object in the database is correct using a hierarchical comparison.

**`TestCases/Orders/PlaceAnOrder.test.json`:**
```json
{
  "TestCaseName": "Place An Order and Verify Hierarchy",
  "DataMapFile": "maps/ecommerce.json",
  "Arrange": [
    {
      "Strategy": "JsonFileSeed",
      "Parameters": { "table": "Products", "dataFile": "data/products.json", "allowIdentityInsert": true }
    },
    {
      "Strategy": "HierarchicalSeed",
      "Parameters": {
        "dataFile": "data/new_customer.json",
        "Outputs": [
          { "VariableName": "newCustomerId", "Source": { "FromTable": "Customers", "SelectColumn": "CustomerId", "OrderByColumn": "CustomerId", "OrderDirection": "DESC" } }
        ]
      }
    }
  ],
  "Act": {
    "Strategy": "ApiCall",
    "Parameters": {
      "Method": "POST",
      "Endpoint": "/api/orders",
      "Payload": {
        "CustomerId": "{newCustomerId}",
        "OrderDate": "2025-08-10T15:30:00Z",
        "TotalAmount": 1274.98,
        "OrderItems": [ { "ProductId": 101, "Quantity": 1 }, { "ProductId": 102, "Quantity": 2 } ]
      }
    }
  },
  "Assert": [
    {
      "Strategy": "HierarchicalCompare",
      "Parameters": {
        "RootEntity": "Order",
        "FindById": "{newOrderId}",
        "ExpectedDataFile": "expected/full-order-expected.json",
        "IncludePaths": [ "Customer", "OrderStatus", "OrderItems.Product" ]
      }
    }
  ]
}
```

### Step 3: Create Supporting Data Files

Create the clean, human-readable data files referenced by your test.

**`data/new_customer.json`:**
```json
{
  "rootTable": "Customers",
  "data": { "FirstName": "Alice", "LastName": "Williams", "Email": "alice.williams@example.com" }
}
```

**`expected/full-order-expected.json`:**
This file represents the entire business object, not just a flat table.
```json
{
  "OrderDate": "2025-08-10T15:30:00Z",
  "TotalAmount": 1274.98,
  "Customer": {
    "FirstName": "Alice",
    "LastName": "Williams",
    "Email": "alice.williams@example.com"
  },
  "OrderStatus": "Processing",
  "OrderItems": [
    { "Quantity": 1, "Product": { "Name": "Laptop" } },
    { "Quantity": 2, "Product": { "Name": "Mouse" } }
  ]
}
```

### Step 4: Write the Test Runner Class

The C# test class remains incredibly simple. Its only job is to point to the directory containing your test cases.

```csharp
// In IntegrationTest.cs
public class IntegrationTest : TestBed<DependencyInjectionFixture>
{
    public IntegrationTest(ITestOutputHelper testOutputHelper, DependencyInjectionFixture fixture)
        : base(testOutputHelper, fixture) { /* ... resolve services ... */ }

    [Theory]
    [DatabaseStateTest("TestCases")]
    public async Task RunDatabaseStateTest(MasterTestDefinition testCase)
    {
        // The test runner logic inside this method will orchestrate the
        // entire Arrange, Act, and Assert flow based on your JSON file.
    }
}
```

By following this pattern, you can build a comprehensive suite of robust, maintainable, and easy-to-read database integration tests.
