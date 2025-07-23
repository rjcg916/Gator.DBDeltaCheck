using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceDemo.Data.Entities;

// The MASTER table
public class Order
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }

    // Foreign key to the Customer lookup table
    public int CustomerId { get; set; }
    public Customer Customer { get; set; }

    // Foreign key to the OrderStatus lookup table
    public int OrderStatusId { get; set; }
    public OrderStatus OrderStatus { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalAmount { get; set; }

    // Navigation property for the detail records
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
