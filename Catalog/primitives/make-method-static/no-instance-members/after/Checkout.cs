namespace Shop
{
    public class Checkout
    {
        public decimal TaxOn(Order order, decimal amount) => Order.Tax(amount);
    }
}
