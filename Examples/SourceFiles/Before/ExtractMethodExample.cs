using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Examples.ExtractMethod;

/// <summary>
/// Example: Order processor with a long method that should have validation extracted.
/// Refactoring: extract-method on lines 25-45 to create ValidateOrderAsync
/// </summary>
public class OrderProcessor
{
    private readonly IInventoryService _inventory;
    private readonly IPaymentGateway _paymentGateway;
    private readonly ILogger _logger;

    public OrderProcessor(IInventoryService inventory, IPaymentGateway paymentGateway, ILogger logger)
    {
        _inventory = inventory;
        _paymentGateway = paymentGateway;
        _logger = logger;
    }

    public async Task<OrderResult> ProcessOrderAsync(Order order)
    {
        // BEGIN EXTRACT: ValidateOrderAsync
        if (order == null)
        {
            _logger.LogWarning("Order is null");
            return new OrderResult { Success = false, Error = "Order cannot be null" };
        }

        if (order.Items == null || !order.Items.Any())
        {
            _logger.LogWarning("Order has no items: {OrderId}", order.Id);
            return new OrderResult { Success = false, Error = "Order must contain at least one item" };
        }

        foreach (var item in order.Items)
        {
            if (item.Quantity <= 0)
            {
                _logger.LogWarning("Invalid quantity for item {ItemId}", item.ProductId);
                return new OrderResult { Success = false, Error = $"Invalid quantity for product {item.ProductId}" };
            }

            if (!await _inventory.IsInStockAsync(item.ProductId, item.Quantity))
            {
                _logger.LogWarning("Insufficient stock for {ItemId}", item.ProductId);
                return new OrderResult { Success = false, Error = $"Product {item.ProductId} is out of stock" };
            }
        }
        // END EXTRACT

        var totalAmount = order.Items.Sum(i => i.Price * i.Quantity);
        var paymentResult = await _paymentGateway.ChargeAsync(order.CustomerId, totalAmount);

        if (!paymentResult.Success)
        {
            return new OrderResult { Success = false, Error = "Payment failed" };
        }

        return new OrderResult { Success = true, OrderId = order.Id };
    }
}

// Supporting types
public class Order
{
    public string Id { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public string ProductId { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class OrderResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? OrderId { get; set; }
}

public interface IInventoryService
{
    Task<bool> IsInStockAsync(string productId, int quantity);
}

public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(string customerId, decimal amount);
}

public class PaymentResult
{
    public bool Success { get; set; }
}

public interface ILogger
{
    void LogWarning(string message, params object[] args);
}
