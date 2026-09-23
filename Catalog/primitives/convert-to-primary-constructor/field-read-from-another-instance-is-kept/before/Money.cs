namespace Shop;

public sealed class Money
{
    private readonly decimal _amount;

    // ISO 4217 code.
    private readonly string _currency;

    public Money(decimal amount, string currency)
    {
        _amount = amount;
        _currency = currency;
    }

    public bool IsMore(Money other) => _amount > other._amount;

    public string Currency() => _currency;
}
