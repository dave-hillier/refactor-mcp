namespace Shop;

public class Money
{
    public string Format(decimal amount, string currency = "GBP")
    {
        return amount + " " + currency;
    }
}
