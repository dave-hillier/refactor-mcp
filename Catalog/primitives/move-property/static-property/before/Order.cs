namespace Shop
{
    public class Order
    {
        public static string Currency { get; set; } = "GBP";

        public string Price(decimal amount) => amount + " " + Currency;
    }

    public static class Settings
    {
    }
}
