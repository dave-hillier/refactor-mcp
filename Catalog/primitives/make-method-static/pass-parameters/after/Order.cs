namespace Shop
{
    public class Order
    {
        public decimal Total { get; set; }

        public string Currency { get; set; }

        public static string Describe(decimal total, string currency, string prefix)
        {
            return prefix + total + " " + currency + " (" + total + ")";
        }

        public string Summary() => Describe(Total, Currency, "Order: ");
    }
}
