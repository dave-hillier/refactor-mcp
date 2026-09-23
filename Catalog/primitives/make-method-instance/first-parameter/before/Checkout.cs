namespace Shop
{
    public class Checkout
    {
        public decimal Pay(Order order) => Order.Discounted(order, 0.1m);
    }
}
