namespace Shop
{
    public class Order
    {
        public string Price(decimal amount) => amount + " " + Settings.Currency;
    }

    public static class Settings
    {
        public static string Currency { get; set; } = "GBP";
    }
}
