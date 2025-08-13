using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceDemo.Data.Entities;

public class Product
{
    public int ProductId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    [Column(TypeName = "decimal(18, 2)")] public decimal Price { get; init; }
}