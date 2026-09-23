namespace Shop
{
    public class Refund
    {
        public Money Amount(Order order) => order.Total / 2;
    }
}
