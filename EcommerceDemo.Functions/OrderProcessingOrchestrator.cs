using System.Net;
using EcommerceDemo.Data.Data;
using EcommerceDemo.Data.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace EcommerceDemo.Functions;

public class OrderProcessingOrchestrator(ECommerceDbContext dbContext)
{
    [Function("GenericOrchestrationStarter")]
    public async Task<HttpResponseData> Start(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orchestrators/{orchestratorName}")]
        HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext,
        string orchestratorName)
    {
        var logger = executionContext.GetLogger(nameof(Start));

        var requestBody = await req.ReadFromJsonAsync<object>();
        if (requestBody == null) return req.CreateResponse(HttpStatusCode.BadRequest);

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            orchestratorName, requestBody);

        logger.LogInformation("Started orchestration '{orchestratorName}' with ID = '{instanceId}'.", orchestratorName,
            instanceId);

        return  client.CreateCheckStatusResponse(req, instanceId);
    }


    [Function(nameof(OrderOrchestrator))]
    public async Task<string> OrderOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(OrderOrchestrator));
        var payload = context.GetInput<OrchestrationPayload>();


        // Validate the input immediately and throw an exception if it's invalid.
        // This will cause the orchestration to fail with a clear error message.
        if (payload is not { CustomerId: > 0 })
        {
            logger.LogError("Invalid input: CustomerId is missing or invalid.");
            throw new ArgumentException("Orchestration payload must include a valid CustomerId.");
        }

        logger.LogInformation("Orchestration started for Customer ID: {CustomerId}", payload.CustomerId);

        // Now we can safely use payload.CustomerId without null checks.
        var newOrderId = await context.CallActivityAsync<int>(nameof(CreateOrderActivity), payload.CustomerId);
        logger.LogInformation("Created new order with ID: {OrderId}", newOrderId);

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
            OrderStatusId = 22,
            TotalAmount = 99.99m
        };

        dbContext.Orders.Add(newOrder);
        await dbContext.SaveChangesAsync();

        return newOrder.OrderId;
    }


    [Function(nameof(UpdateCustomerEmailActivity))]
    public async Task UpdateCustomerEmailActivity([ActivityTrigger] OrchestrationPayload payload,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger(nameof(UpdateCustomerEmailActivity));
        logger.LogInformation("Updating email for customer {CustomerId}.", payload.CustomerId);

        var customer = await dbContext.Customers.FindAsync(payload.CustomerId);
        if (customer != null)
        {
            customer.Email = payload.NewEmail;
            await dbContext.SaveChangesAsync();
        }
    }
}

public class OrchestrationPayload
{
    public int CustomerId { get; set; }
    public required string NewEmail { get; set; }
}