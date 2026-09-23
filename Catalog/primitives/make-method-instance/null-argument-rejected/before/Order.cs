namespace Shop
{
    public class Order
    {
        public string Number { get; set; }

        public static string NumberOf(Order order) => order == null ? "none" : order.Number;

        public static string Nothing() => NumberOf(null);
    }
}
