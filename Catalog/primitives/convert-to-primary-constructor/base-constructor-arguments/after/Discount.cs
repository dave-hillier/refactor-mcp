namespace Shop;

public abstract class Adjustment
{
    protected Adjustment(decimal amount)
    {
        Amount = amount;
    }

    public decimal Amount { get; }
}

public sealed class Discount(decimal amount, string code) : Adjustment(-amount)
{
    public string Describe() => code + ": " + Amount;
}
