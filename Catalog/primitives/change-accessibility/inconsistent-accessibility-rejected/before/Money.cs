namespace Shop;

public class Money
{
    public decimal Amount { get; set; }
}

public class Till
{
    public Money Total()
    {
        return new Money();
    }
}
