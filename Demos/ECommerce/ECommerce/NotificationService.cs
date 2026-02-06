namespace ECommerce;

/// <summary>
/// Notification handling — monolithic class that's a good candidate for
/// interface extraction and decorator wrapping.
///
/// Refactoring opportunities:
///   - extract-interface: extract INotificationService from public methods
///   - extract-decorator: wrap SendOrderConfirmation with logging/metrics decorator
///   - convert-to-extension-method: FormatCurrency is a utility that works on decimal
///   - move-type-to-file: NotificationTemplate is defined here but deserves its own file
///   - cleanup-usings: System.Diagnostics is imported but unused
/// </summary>
using System.Diagnostics;

public class NotificationService
{
    private readonly EmailService _emailService;
    private readonly List<string> _notificationLog = new();

    public NotificationService(EmailService emailService)
    {
        _emailService = emailService;
    }

    public void SendOrderConfirmation(string email, string orderId, decimal total)
    {
        var subject = $"Order Confirmation - {orderId}";
        var body = $"Thank you for your order!\n\nOrder ID: {orderId}\nTotal: {FormatCurrency(total)}\n\nYour order is being processed.";

        _emailService.Send(email, subject, body);
        _notificationLog.Add($"[{DateTime.UtcNow:O}] ORDER_CONFIRM: {orderId} -> {email}");
    }

    public void SendShippingNotification(string email, string orderId, string trackingNumber, int estimatedDays)
    {
        var subject = $"Your Order {orderId} Has Shipped!";
        var body = $"Great news! Your order is on its way.\n\nTracking: {trackingNumber}\nEstimated delivery: {estimatedDays} business days";

        _emailService.Send(email, subject, body);
        _notificationLog.Add($"[{DateTime.UtcNow:O}] SHIPPING: {orderId} -> {email}");
    }

    public void SendPaymentFailedNotification(string email, string orderId, string reason)
    {
        var subject = $"Payment Issue - Order {orderId}";
        var body = $"We were unable to process your payment.\n\nOrder ID: {orderId}\nReason: {reason}\n\nPlease update your payment method.";

        _emailService.Send(email, subject, body);
        _notificationLog.Add($"[{DateTime.UtcNow:O}] PAYMENT_FAIL: {orderId} -> {email}");
    }

    public void SendTierUpgradeNotification(string email, string customerName, CustomerTier newTier)
    {
        var subject = $"Congratulations {customerName}! You've been upgraded!";
        var discountPercent = newTier switch
        {
            CustomerTier.Silver => 5,
            CustomerTier.Gold => 10,
            CustomerTier.Platinum => 15,
            _ => 0
        };
        var body = $"You've reached {newTier} status!\n\nYou now enjoy {discountPercent}% off all orders.";

        _emailService.Send(email, subject, body);
        _notificationLog.Add($"[{DateTime.UtcNow:O}] TIER_UPGRADE: {newTier} -> {email}");
    }

    /// <summary>
    /// Utility method that operates on decimal — should be an extension method.
    /// It doesn't use any instance state.
    /// </summary>
    public string FormatCurrency(decimal amount)
    {
        if (amount < 0)
            return $"-${Math.Abs(amount):N2}";
        return $"${amount:N2}";
    }

    public List<string> GetNotificationLog() => new(_notificationLog);
}

/// <summary>
/// This type is defined in NotificationService.cs but should be in its own file.
/// move-type-to-file candidate.
/// </summary>
public class NotificationTemplate
{
    public string TemplateName { get; set; } = "";
    public string Subject { get; set; } = "";
    public string BodyTemplate { get; set; } = "";
    public Dictionary<string, string> Placeholders { get; set; } = new();

    public string Render(Dictionary<string, string> values)
    {
        var result = BodyTemplate;
        foreach (var (key, value) in values)
        {
            result = result.Replace($"{{{{{key}}}}}", value);
        }
        return result;
    }
}
