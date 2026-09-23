namespace Shop
{
    public static class Receipts
    {
        /// <summary>A line for the receipt.</summary>
        public static string Describe(Order order, string prefix)
        {
            // The total is shown without rounding.
            return prefix + order._total + " " + order.Currency;
        }
    }
}
