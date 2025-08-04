namespace ECommerceDemo.Data.Entities;

public class OrderStatus
{
    public int OrderStatusId { get; init; }
    public string StatusName { get; init; } // e.g., "Pending", "Shipped", "Delivered"
}