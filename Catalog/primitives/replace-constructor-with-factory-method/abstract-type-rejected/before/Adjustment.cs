namespace Shop;

public abstract class Adjustment
{
    protected Adjustment(decimal amount)
    {
        Amount = amount;
    }

    public decimal Amount { get; }
}
