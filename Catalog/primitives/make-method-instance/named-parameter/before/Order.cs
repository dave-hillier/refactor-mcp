namespace Shop
{
    public class Order
    {
        public string Number { get; set; }

        public static string Label(string prefix, Order order) => prefix + order.Number;
    }
}
