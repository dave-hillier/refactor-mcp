namespace Shop
{
    public class Checkout
    {
        public int Adjust(Order order, int extra)
        {
            order.SetQuantity(2);
            order.SetQuantity(order.GetQuantity() + extra * 2);
            order.SetQuantity(order.GetQuantity() + 1);
            return order.GetQuantity();
        }
    }
}
