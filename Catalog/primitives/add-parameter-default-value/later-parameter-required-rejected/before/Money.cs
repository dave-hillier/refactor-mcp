namespace Shop;

public class Money
{
    public string Format(decimal amount, string currency)
    {
        return amount + " " + currency;
    }
}
