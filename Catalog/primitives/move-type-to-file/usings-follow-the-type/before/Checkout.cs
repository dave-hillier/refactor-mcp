namespace Shop
{
    public class Checkout
    {
        public string Pay(Order order) => new Receipt().Print(order);
    }
}
