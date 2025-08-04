using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceDemo.Data.Entities;

public class Product
{
    public int ProductId { get; init; }
    public string Name { get; init; }
    public string Description { get; init; }

    [Column(TypeName = "decimal(18, 2)")] public decimal Price { get; init; }
}