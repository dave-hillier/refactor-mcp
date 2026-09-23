namespace Shop
{
    public class Order
    {
        public string Number { get; set; }

        public decimal Total { get; set; }

        public string Describe()
        {
            return OrderExtensions.Describe(this);
        }
    }

    public static class OrderExtensions
    {
        public static string Describe(this Order order)
        {
            return order.Number + ": " + order.Total;
        }
    }
}
