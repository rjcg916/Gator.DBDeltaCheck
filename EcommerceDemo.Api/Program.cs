using ECommerceDemo.Data;
using ECommerceDemo.Data.Data;
using ECommerceDemo.Data.Entities;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Configure Services ---

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Get the connection string from appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Register the ECommerceDbContext with the dependency injection container
builder.Services.AddDbContext<ECommerceDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

// --- 2. Configure the HTTP request pipeline. ---

// Use Swagger for API documentation and testing
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// --- 3. Define API Endpoints ---

// -- Customer Endpoints --
var customerGroup = app.MapGroup("/api/customers");

customerGroup.MapGet("/", async (ECommerceDbContext db) =>
    await db.Customers.ToListAsync());

customerGroup.MapGet("/{id}", async (int id, ECommerceDbContext db) =>
    await db.Customers.FindAsync(id)
        is Customer customer
            ? Results.Ok(customer)
            : Results.NotFound());

customerGroup.MapPost("/", async (Customer customer, ECommerceDbContext db) =>
{
    db.Customers.Add(customer);
    await db.SaveChangesAsync();
    return Results.Created($"/api/customers/{customer.CustomerId}", customer);
});

customerGroup.MapPut("/{id}", async (int id, Customer inputCustomer, ECommerceDbContext db) =>
{
    var customer = await db.Customers.FindAsync(id);
    if (customer is null) return Results.NotFound();

    customer.FirstName = inputCustomer.FirstName;
    customer.LastName = inputCustomer.LastName;
    customer.Email = inputCustomer.Email;

    await db.SaveChangesAsync();
    return Results.NoContent();
});

customerGroup.MapDelete("/{id}", async (int id, ECommerceDbContext db) =>
{
    if (await db.Customers.FindAsync(id) is Customer customer)
    {
        db.Customers.Remove(customer);
        await db.SaveChangesAsync();
        return Results.Ok(customer);
    }
    return Results.NotFound();
});

// Deletes a customer by first and last name using query parameters.
// Example: DELETE /api/customers?firstName=Jane&lastName=Smith
customerGroup.MapDelete("/", async (string firstName, string lastName, ECommerceDbContext db) =>
{
    var customer = await db.Customers
        .FirstOrDefaultAsync(c => c.FirstName == firstName && c.LastName == lastName);

    if (customer is null)
    {
        return Results.NotFound($"No customer found with the name '{firstName} {lastName}'.");
    }

    db.Customers.Remove(customer);
    await db.SaveChangesAsync();

    return Results.Ok(customer);
});


// -- Order Endpoints --
var orderGroup = app.MapGroup("/api/orders");

// Get all orders with customer details
orderGroup.MapGet("/", async (ECommerceDbContext db) =>
    await db.Orders.Include(o => o.Customer).ToListAsync());

// Get a single order with all details (customer and order items)
orderGroup.MapGet("/{id}", async (int id, ECommerceDbContext db) =>
{
    var order = await db.Orders
        .Include(o => o.Customer)
        .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
        .FirstOrDefaultAsync(o => o.OrderId == id);

    return order is not null ? Results.Ok(order) : Results.NotFound();
});

// Create a new order
orderGroup.MapPost("/", async (Order newOrder, ECommerceDbContext db) =>
{
    // In a real app, you would calculate the TotalAmount based on items.
    // Here we assume it's provided or calculated on the client.
    db.Orders.Add(newOrder);
    await db.SaveChangesAsync();
    return Results.Created($"/api/orders/{newOrder.OrderId}", newOrder);
});


// --- 4. Run the Application ---
app.Run();
