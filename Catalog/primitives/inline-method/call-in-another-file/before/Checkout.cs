namespace Shop
{
    public class Checkout
    {
        public string Label(Order order) => "Buying " + order.Describe();
    }
}
