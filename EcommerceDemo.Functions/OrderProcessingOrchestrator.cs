using ECommerceDemo.Data;
using ECommerceDemo.Data.Data;
using ECommerceDemo.Data.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace ECommerceDemo.Functions;

public class OrderProcessingOrchestrator
{
    private readonly ECommerceDbContext _dbContext;

    public OrderProcessingOrchestrator(ECommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }


    [Function("GenericOrchestrationStarter")]
    public async Task<HttpResponseData> Start(
        // The route now has a template to accept the orchestrator name from the URL
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orchestrators/{orchestratorName}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext,
        string orchestratorName) // The name from the route is passed in here
    {
        var logger = executionContext.GetLogger(nameof(Start));

        var requestBody = await req.ReadFromJsonAsync<object>(); // Read a generic object
        if (requestBody == null)
        {
            return req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
        }

        // Use the orchestratorName from the URL to start the correct orchestration
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            orchestratorName, requestBody);

        logger.LogInformation("Started orchestration '{orchestratorName}' with ID = '{instanceId}'.", orchestratorName, instanceId);

        return client.CreateCheckStatusResponse(req, instanceId);
    }


    // This function defines the workflow. It calls the activity functions.
    [Function(nameof(RunOrchestrator))]
    public async Task<string> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(RunOrchestrator));
        var payload = context.GetInput<OrchestrationPayload>();

        logger.LogInformation("Orchestration started for Customer ID: {CustomerId}", payload.CustomerId);

        // Step 1: Create a new order
        var newOrderId = await context.CallActivityAsync<int>(nameof(CreateOrderActivity), payload.CustomerId);
        logger.LogInformation("Created new order with ID: {OrderId}", newOrderId);

        // Step 2: Update the customer's email
        await context.CallActivityAsync(nameof(UpdateCustomerEmailActivity), payload);
        logger.LogInformation("Updated email for Customer ID: {CustomerId}", payload.CustomerId);

        return $"Order {newOrderId} processed successfully for customer {payload.CustomerId}.";
    }


    [Function(nameof(CreateOrderActivity))]
    public async Task<int> CreateOrderActivity([ActivityTrigger] int customerId, FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger(nameof(CreateOrderActivity));
        logger.LogInformation("Creating order for customer {CustomerId}.", customerId);

        var newOrder = new Order
        {
            CustomerId = customerId,
            OrderDate = DateTime.UtcNow,
            OrderStatusId = 2, // "Processing"
            TotalAmount = 99.99m // A dummy amount
        };

        _dbContext.Orders.Add(newOrder);
        await _dbContext.SaveChangesAsync();

        return newOrder.OrderId;
    }


    [Function(nameof(UpdateCustomerEmailActivity))]
    public async Task UpdateCustomerEmailActivity([ActivityTrigger] OrchestrationPayload payload, FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger(nameof(UpdateCustomerEmailActivity));
        logger.LogInformation("Updating email for customer {CustomerId}.", payload.CustomerId);

        var customer = await _dbContext.Customers.FindAsync(payload.CustomerId);
        if (customer != null)
        {
            customer.Email = payload.NewEmail;
            await _dbContext.SaveChangesAsync();
        }
    }
}

// A simple class to model the input payload for the orchestration
public class OrchestrationPayload
{
    public int CustomerId { get; set; }
    public string NewEmail { get; set; }
}
