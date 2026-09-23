namespace Shop
{
    public class Checkout
    {
        public int Add(Order order)
        {
            order.Quantity = 2;
            order.Quantity += 1;
            return order.Quantity;
        }
    }
}
