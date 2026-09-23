namespace Shop;

public abstract class Adjustment(decimal amount)
{
    public decimal Amount { get; } = amount;
}

public sealed class Discount(decimal amount, string code) : Adjustment(-amount)
{
    public string Describe() => code + ": " + Amount;
}
