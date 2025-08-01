using ECommerceDemo.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerceDemo.Data.Data;

public class ECommerceDbContext : DbContext
{
    public ECommerceDbContext(DbContextOptions<ECommerceDbContext> options) : base(options) { }

    // DbSets for all our entities
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<OrderStatus> OrderStatuses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure relationships and constraints here if needed.
        // EF Core's conventions handle most of this for us, but
        // you can be explicit.

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.OrderStatus);

        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.Order)
            .WithMany(o => o.OrderItems)
            .HasForeignKey(oi => oi.OrderId);

        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.Product);

        // Seed initial data for lookup tables
        modelBuilder.Entity<OrderStatus>().HasData(
            new OrderStatus { OrderStatusId = 1, StatusName = "Pending" },
            new OrderStatus { OrderStatusId = 2, StatusName = "Processing" },
            new OrderStatus { OrderStatusId = 3, StatusName = "Shipped" },
            new OrderStatus { OrderStatusId = 4, StatusName = "Delivered" },
            new OrderStatus { OrderStatusId = 5, StatusName = "Cancelled" }
        );
    }
}

