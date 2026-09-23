namespace Shop;

public abstract class Adjustment
{
    protected Adjustment(decimal amount)
    {
        Amount = amount;
    }

    public decimal Amount { get; }
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
