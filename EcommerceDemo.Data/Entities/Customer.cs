namespace ECommerceDemo.Data.Entities;

public class Customer
{
    public int CustomerId { get; init; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }

    // Navigation property for related orders
    public ICollection<Order> Orders { get; init; } = new List<Order>();
}