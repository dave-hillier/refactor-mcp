namespace Shop
{
    public class Checkout
    {
        public decimal Total(Order order) => order.Price(3, 10m) + order.Price(1, 2000m);
    }
}
