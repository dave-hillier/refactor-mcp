namespace Shop;

public class Money
{
    public Money(decimal amount) : this(amount, "EUR")
    {
    }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Zero() => new Money(0m);

    public static Money Dollars(decimal amount) => new Money(amount, "USD");
}
