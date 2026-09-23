namespace Shop
{
    public class Checkout
    {
        public int Pay(Order order) => order.Total();
    }
}
