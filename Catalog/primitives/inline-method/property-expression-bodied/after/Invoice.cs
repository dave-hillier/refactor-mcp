namespace Shop
{
    public class Invoice
    {
        public decimal Due(Order order) => (order.Subtotal + order.Tax) * 1.1m;
    }
}
