namespace ECommerce;

/// <summary>
/// Payment processing — used as a dependency by OrderProcessor.
/// Demonstrates a class that should have an interface extracted for testability.
/// </summary>
public class PaymentGateway
{
    private readonly Dictionary<string, decimal> _balances = new();
    private readonly List<PaymentTransaction> _transactions = new();

    public void SetBalance(string customerId, decimal balance)
    {
        _balances[customerId] = balance;
    }

    public PaymentResult Charge(string customerId, decimal amount)
    {
        var balance = _balances.GetValueOrDefault(customerId, 0m);

        if (amount <= 0)
            return new PaymentResult { Success = false, ErrorMessage = "Invalid amount" };

        if (balance < amount)
            return new PaymentResult { Success = false, ErrorMessage = "Insufficient funds" };

        _balances[customerId] = balance - amount;

        var transaction = new PaymentTransaction
        {
            TransactionId = $"TXN-{Guid.NewGuid().ToString("N")[..8]}",
            CustomerId = customerId,
            Amount = amount,
            Timestamp = DateTime.UtcNow,
            Type = "CHARGE"
        };
        _transactions.Add(transaction);

        return new PaymentResult
        {
            Success = true,
            TransactionId = transaction.TransactionId,
            AmountCharged = amount
        };
    }

    public PaymentResult Refund(string transactionId, decimal amount)
    {
        var original = _transactions.FirstOrDefault(t => t.TransactionId == transactionId);
        if (original == null)
            return new PaymentResult { Success = false, ErrorMessage = "Transaction not found" };

        if (amount > original.Amount)
            return new PaymentResult { Success = false, ErrorMessage = "Refund exceeds original charge" };

        _balances[original.CustomerId] = _balances.GetValueOrDefault(original.CustomerId, 0m) + amount;

        var refundTxn = new PaymentTransaction
        {
            TransactionId = $"REF-{Guid.NewGuid().ToString("N")[..8]}",
            CustomerId = original.CustomerId,
            Amount = amount,
            Timestamp = DateTime.UtcNow,
            Type = "REFUND"
        };
        _transactions.Add(refundTxn);

        return new PaymentResult
        {
            Success = true,
            TransactionId = refundTxn.TransactionId,
            AmountCharged = -amount
        };
    }

    public List<PaymentTransaction> GetTransactionHistory(string customerId)
    {
        return _transactions.Where(t => t.CustomerId == customerId).ToList();
    }
}

public class PaymentTransaction
{
    public string TransactionId { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = "";
}
