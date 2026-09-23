namespace Shop
{
    public class Checkout
    {
        public int Pay(Order order) => order.TotalWithDiscount(2) + order.Total();
    }
}
