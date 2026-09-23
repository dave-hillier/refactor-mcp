namespace Shop;

public class Money
{
    public Money(decimal amount) : this(amount, "EUR")
    {
    }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Of(decimal amount, string currency) => new Money(amount, currency);

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Zero() => new Money(0m);

    public static Money Dollars(decimal amount) => Money.Of(amount, "USD");
}
