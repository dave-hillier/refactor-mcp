namespace Shop
{
    public class Checkout
    {
        public string Receipt(Order order) => Order.Describe(order, "Paid: ");
    }
}
