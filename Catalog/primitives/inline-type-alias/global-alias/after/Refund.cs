namespace Shop
{
    public class Refund
    {
        public decimal Amount(Order order) => order.Total / 2;
    }
}
