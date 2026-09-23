namespace Shop
{
    public class Order
    {
        public decimal Total { get; set; }

        public string Currency { get; set; }

        public string Describe(string prefix)
        {
            return prefix + Total + " " + Currency + " (" + Total + ")";
        }

        public string Summary() => Describe("Order: ");
    }
}
