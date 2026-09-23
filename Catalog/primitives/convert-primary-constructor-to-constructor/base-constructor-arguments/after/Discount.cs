namespace Shop;

public abstract class Adjustment(decimal amount)
{
    public decimal Amount { get; } = amount;
}

public sealed class Discount : Adjustment
{
    private readonly string _code;

    public Discount(decimal amount, string code) : base(-amount)
    {
        _code = code;
    }

    public string Describe() => _code + ": " + Amount;
}
