namespace Shop;

public readonly struct Money
{
    public Money(decimal amount) => Amount = amount;

    public decimal Amount { get; }

    public static Money operator +(Money left, Money right) => new Money(left.Amount + right.Amount);
}
