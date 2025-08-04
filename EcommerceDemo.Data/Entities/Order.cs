using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceDemo.Data.Entities;

public class Order
{
    public int OrderId { get; init; }
    public DateTime OrderDate { get; init; }

    // Foreign key to the Customer lookup table
    public int CustomerId { get; init; }
    public Customer Customer { get; init; }

    // Foreign key to the OrderStatus lookup table
    public int OrderStatusId { get; init; }
    public OrderStatus OrderStatus { get; init; }

    [Column(TypeName = "decimal(18, 2)")] public decimal TotalAmount { get; init; }

    // Navigation property for the detail records
    public ICollection<OrderItem> OrderItems { get; init; } = new List<OrderItem>();
}