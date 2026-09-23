using System;

namespace Shop;

public sealed record Money
{
    private readonly int _scale = 2;

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public decimal Rounded => decimal.Round(Amount, _scale);
}
