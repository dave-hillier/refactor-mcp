namespace Shop
{
    public class Checkout
    {
        public string Label(Order order) => "Buying " + $"{order.Name} x {order.Quantity}";
    }
}
