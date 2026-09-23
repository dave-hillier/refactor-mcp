namespace Shop
{
    public class Order
    {
        public string Number { get; set; }
    }

    public static class OrderExtensions
    {
        public static string Describe(this Order order) => order.Number;
    }
}
