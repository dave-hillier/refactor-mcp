namespace Shop
{
    public class Checkout
    {
        public int Pay(Order order) => order.Total(2) + order.Total();
    }
}
