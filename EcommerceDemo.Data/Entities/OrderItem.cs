using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceDemo.Data.Entities;

public class OrderItem
{
    public int OrderItemId { get; init; }

    // Foreign key to the Order master table
    public int OrderId { get; init; }
    public required Order Order { get; init; }

    // Foreign key to the Product lookup table
    public int ProductId { get; init; }
    public required Product Product { get; init; }

    public int Quantity { get; init; }

    [Column(TypeName = "decimal(18, 2)")] public decimal UnitPrice { get; init; } // Price at the time of order
}