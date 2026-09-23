namespace Shop
{
    public class Checkout
    {
        public decimal Pay(Order order) => order.Discounted(0.1m);
    }
}
