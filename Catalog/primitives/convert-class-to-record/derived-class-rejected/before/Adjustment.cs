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
    public Discount(decimal amount) : base(amount)
    {
    }
}
