namespace ECommerceDemo.Data.Entities;

// A LOOKUP table
public class OrderStatus
{
    public int OrderStatusId { get; set; }
    public string StatusName { get; set; } // e.g., "Pending", "Shipped", "Delivered"
}

