namespace Shop
{
    public class Checkout
    {
        public int Adjust(Order order, int extra)
        {
            order.Quantity = 2;
            order.Quantity += extra * 2;
            order.Quantity++;
            return order.Quantity;
        }
    }
}
