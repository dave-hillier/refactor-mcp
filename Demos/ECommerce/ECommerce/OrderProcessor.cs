using System.Text;

namespace ECommerce;

/// <summary>
/// God class: handles validation, pricing, payment, shipping, notifications, and logging.
///
/// Refactoring opportunities:
///   - extract-method: ProcessOrder is 100+ lines with distinct phases
///   - move-instance-method: FormatAuditLogEntry belongs in AuditLogger
///   - safe-delete-method: LegacyExportXml is never called
///   - safe-delete-field: _migrationTimestamp is unused
///   - introduce-parameter: hardcoded tax rate 0.08m
///   - make-field-readonly: _paymentGateway, _auditLogger never reassigned
///   - rename-symbol: poorly named 'x' variable
/// </summary>
public class OrderProcessor
{
    private PaymentGateway _paymentGateway;
    private AuditLogger _auditLogger;
    private readonly NotificationService _notificationService;
    private readonly InventoryManager _inventory;
    private DateTime _migrationTimestamp;
    private int _processedCount;

    public OrderProcessor(
        PaymentGateway paymentGateway,
        AuditLogger auditLogger,
        NotificationService notificationService,
        InventoryManager inventory)
    {
        _paymentGateway = paymentGateway;
        _auditLogger = auditLogger;
        _notificationService = notificationService;
        _inventory = inventory;
    }

    /// <summary>
    /// Monolithic method that does everything — prime candidate for extract-method.
    /// </summary>
    public OrderResult ProcessOrder(Order order, Customer customer)
    {
        // ── Phase 1: Validation (lines to extract into ValidateOrder) ──
        if (order == null)
            return new OrderResult { Success = false, Error = "Order is null" };

        if (customer == null)
            return new OrderResult { Success = false, Error = "Customer is null" };

        if (order.Items == null || order.Items.Count == 0)
            return new OrderResult { Success = false, Error = "Order contains no items" };

        foreach (var item in order.Items)
        {
            if (item.Quantity <= 0)
                return new OrderResult { Success = false, Error = $"Invalid quantity for {item.ProductName}" };

            if (item.UnitPrice < 0)
                return new OrderResult { Success = false, Error = $"Negative price for {item.ProductName}" };

            var stock = _inventory.GetStockLevel(item.ProductId);
            if (stock < item.Quantity)
                return new OrderResult { Success = false, Error = $"Insufficient stock for {item.ProductName}: need {item.Quantity}, have {stock}" };
        }

        if (string.IsNullOrWhiteSpace(order.ShippingAddress.Street) ||
            string.IsNullOrWhiteSpace(order.ShippingAddress.City) ||
            string.IsNullOrWhiteSpace(order.ShippingAddress.ZipCode))
            return new OrderResult { Success = false, Error = "Incomplete shipping address" };

        // ── Phase 2: Pricing calculation ──
        decimal subtotal = 0m;
        foreach (var item in order.Items)
        {
            subtotal += item.UnitPrice * item.Quantity;
        }

        // Apply customer tier discount
        decimal x = customer.Tier switch
        {
            CustomerTier.Silver => 0.05m,
            CustomerTier.Gold => 0.10m,
            CustomerTier.Platinum => 0.15m,
            _ => 0m
        };
        subtotal *= (1 - x);

        // Apply coupon if present
        if (!string.IsNullOrEmpty(order.CouponCode))
        {
            if (order.CouponCode == "SAVE10")
                subtotal *= 0.90m;
            else if (order.CouponCode == "SAVE20")
                subtotal *= 0.80m;
            else if (order.CouponCode == "HALFOFF")
                subtotal *= 0.50m;
        }

        // Tax calculation — hardcoded rate should be a parameter
        decimal taxAmount = subtotal * 0.08m;
        decimal totalAmount = subtotal + taxAmount;

        // Shipping cost based on weight
        decimal totalWeight = order.Items.Sum(i => i.Weight * i.Quantity);
        decimal shippingCost;
        if (totalWeight <= 1.0m)
            shippingCost = 5.99m;
        else if (totalWeight <= 5.0m)
            shippingCost = 9.99m;
        else if (totalWeight <= 20.0m)
            shippingCost = 14.99m;
        else
            shippingCost = 24.99m;

        if (order.ShippingAddress.Country != "US")
            shippingCost *= 2.5m;

        totalAmount += shippingCost;

        // ── Phase 3: Payment processing ──
        var paymentResult = _paymentGateway.Charge(customer.Id, totalAmount);
        if (!paymentResult.Success)
            return new OrderResult { Success = false, Error = $"Payment failed: {paymentResult.ErrorMessage}" };

        // ── Phase 4: Inventory reservation ──
        foreach (var item in order.Items)
        {
            _inventory.ReserveStock(item.ProductId, item.Quantity);
        }

        order.Status = OrderStatus.PaymentProcessed;
        _processedCount++;

        // ── Phase 5: Audit logging (method belongs in AuditLogger) ──
        var logEntry = FormatAuditLogEntry(order, customer, totalAmount, paymentResult.TransactionId ?? "N/A");
        _auditLogger.WriteEntry(logEntry);

        // ── Phase 6: Customer notification ──
        _notificationService.SendOrderConfirmation(customer.Email, order.Id, totalAmount);

        return new OrderResult
        {
            Success = true,
            OrderId = order.Id,
            TotalCharged = totalAmount,
            TransactionId = paymentResult.TransactionId
        };
    }

    /// <summary>
    /// This method formats audit log entries but it really belongs in AuditLogger.
    /// It only uses OrderProcessor state minimally — prime candidate for move-instance-method.
    /// </summary>
    public string FormatAuditLogEntry(Order order, Customer customer, decimal amount, string transactionId)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Order Audit Entry ===");
        sb.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
        sb.AppendLine($"Order ID: {order.Id}");
        sb.AppendLine($"Customer: {customer.Name} ({customer.Id})");
        sb.AppendLine($"Items: {order.Items.Count}");
        sb.AppendLine($"Amount: {amount:C}");
        sb.AppendLine($"Transaction: {transactionId}");
        sb.AppendLine($"Status: {order.Status}");
        sb.AppendLine($"Processor Count: {_processedCount}");
        sb.AppendLine($"========================");
        return sb.ToString();
    }

    /// <summary>
    /// Legacy method that is never called anywhere — safe-delete candidate.
    /// </summary>
    public string LegacyExportXml(Order order)
    {
        return $"<order><id>{order.Id}</id><status>{order.Status}</status></order>";
    }
}

public class OrderResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? OrderId { get; set; }
    public decimal TotalCharged { get; set; }
    public string? TransactionId { get; set; }
}
