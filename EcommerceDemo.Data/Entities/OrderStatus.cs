namespace EcommerceDemo.Data.Entities;

public class OrderStatus
{
    public int OrderStatusId { get; init; }
    public required string StatusName { get; init; } // e.g., "Pending", "Shipped", "Delivered"
}