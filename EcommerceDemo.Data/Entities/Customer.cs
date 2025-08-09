namespace ECommerceDemo.Data.Entities;

public class Customer
{
    public int CustomerId { get; init; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }

    // Navigation property for related orders
    public ICollection<Order> Orders { get; init; } = new List<Order>();
}