namespace Shop;

public sealed class Money(decimal amount, string currency)
{
    private readonly decimal _amount = amount;

    // ISO 4217 code.
    private readonly string _currency = currency;

    public bool IsMore(Money other) => _amount > other._amount;

    public string Currency() => _currency;
}
