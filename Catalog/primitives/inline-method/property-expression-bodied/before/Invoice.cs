namespace Shop
{
    public class Invoice
    {
        public decimal Due(Order order) => order.Total * 1.1m;
    }
}
