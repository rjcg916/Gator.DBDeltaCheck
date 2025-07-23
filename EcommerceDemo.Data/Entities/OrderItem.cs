using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceDemo.Data.Entities;

// A DETAIL table
public class OrderItem
{
    public int OrderItemId { get; set; }

    // Foreign key to the Order master table
    public int OrderId { get; set; }
    public Order Order { get; set; }

    // Foreign key to the Product lookup table
    public int ProductId { get; set; }
    public Product Product { get; set; }

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitPrice { get; set; } // Price at the time of order
}
