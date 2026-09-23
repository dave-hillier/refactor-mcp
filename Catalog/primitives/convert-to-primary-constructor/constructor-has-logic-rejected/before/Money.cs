using System;

namespace Shop;

public sealed class Money
{
    private readonly decimal _amount;

    public Money(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        _amount = amount;
    }

    public decimal Amount => _amount;
}
