namespace EcommerceDemo.Api.DTOs;

public class OrderDto
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }

    public required CustomerDto Customer { get; set; }
}