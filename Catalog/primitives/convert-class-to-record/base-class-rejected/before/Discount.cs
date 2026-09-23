namespace Shop;

public abstract class Adjustment
{
}

public sealed class Discount : Adjustment
{
    public Discount(decimal amount)
    {
        Amount = amount;
    }

    public decimal Amount { get; }
}
