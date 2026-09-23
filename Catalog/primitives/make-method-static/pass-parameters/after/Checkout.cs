namespace Shop
{
    public class Checkout
    {
        public string Receipt(Order order) => Order.Describe(order.Total, order.Currency, "Paid: ");
    }
}
