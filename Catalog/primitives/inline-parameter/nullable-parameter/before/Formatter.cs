namespace Shop;

public class Formatter
{
    public string Money(decimal amount, string? culture)
    {
        return amount + " " + (culture ?? "en");
    }

    public string Price()
    {
        return Money(9.99m, "en-GB");
    }
}
