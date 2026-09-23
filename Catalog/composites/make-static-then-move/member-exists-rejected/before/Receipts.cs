namespace Shop
{
    public static class Receipts
    {
        public static string Describe(Order order, string prefix) => prefix + order.Currency;
    }
}
