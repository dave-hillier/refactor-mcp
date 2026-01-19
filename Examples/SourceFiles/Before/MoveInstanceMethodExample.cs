using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Examples.MoveInstanceMethod;

/// <summary>
/// Example: OrderService with pricing methods that exhibit "feature envy" - they belong in a PricingCalculator.
/// Refactoring: move-instance-method for CalculateSubtotal, CalculateTax, CalculateShipping to PricingCalculator
/// </summary>
public class OrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryService _inventoryService;

    public OrderService(IOrderRepository orderRepository, IInventoryService inventoryService)
    {
        _orderRepository = orderRepository;
        _inventoryService = inventoryService;
    }

    public async Task<Order> CreateOrderAsync(OrderRequest request)
    {
        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = request.CustomerId,
            Items = request.Items,
            CreatedAt = DateTime.UtcNow
        };

        // These calculations should be in a PricingCalculator class
        order.Subtotal = CalculateSubtotal(order.Items);
        order.Tax = CalculateTax(order.Subtotal, request.ShippingAddress.State);
        order.ShippingCost = CalculateShipping(order.Items, request.ShippingAddress);
        order.Total = order.Subtotal + order.Tax + order.ShippingCost;

        await _orderRepository.SaveAsync(order);
        return order;
    }

    // BEGIN MOVE: These methods should be moved to PricingCalculator
    public decimal CalculateSubtotal(List<OrderItem> items)
    {
        return items.Sum(item => item.UnitPrice * item.Quantity);
    }

    public decimal CalculateTax(decimal subtotal, string state)
    {
        var taxRates = new Dictionary<string, decimal>
        {
            { "CA", 0.0725m },
            { "NY", 0.08m },
            { "TX", 0.0625m },
            { "WA", 0.065m }
        };

        var rate = taxRates.GetValueOrDefault(state, 0.05m);
        return Math.Round(subtotal * rate, 2);
    }

    public decimal CalculateShipping(List<OrderItem> items, Address address)
    {
        var totalWeight = items.Sum(i => i.Weight * i.Quantity);

        decimal baseRate = address.Country == "US" ? 5.99m : 19.99m;
        decimal weightCharge = totalWeight * 0.50m;

        if (items.Sum(i => i.UnitPrice * i.Quantity) > 100)
        {
            return 0; // Free shipping over $100
        }

        return Math.Round(baseRate + weightCharge, 2);
    }
    // END MOVE
}

// Supporting types
public class OrderRequest
{
    public string CustomerId { get; set; } = "";
    public List<OrderItem> Items { get; set; } = new();
    public Address ShippingAddress { get; set; } = new();
}

public class Order
{
    public string Id { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public List<OrderItem> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
}

public class OrderItem
{
    public string ProductId { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Weight { get; set; }
}

public class Address
{
    public string State { get; set; } = "";
    public string Country { get; set; } = "";
}

public interface IOrderRepository
{
    Task SaveAsync(Order order);
}

public interface IInventoryService
{
    Task<bool> CheckAvailabilityAsync(string productId, int quantity);
}
