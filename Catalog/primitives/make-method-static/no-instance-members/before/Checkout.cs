namespace Shop
{
    public class Checkout
    {
        public decimal TaxOn(Order order, decimal amount) => order.Tax(amount);
    }
}
