namespace Shop;

public class Formatter
{
    public string Money(decimal amount)
    {
        return amount + " " + ("en-GB" ?? "en");
    }

    public string Price()
    {
        return Money(9.99m);
    }
}
