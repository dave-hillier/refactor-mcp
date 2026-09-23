namespace Shop
{
    public class Checkout
    {
        public string Receipt(Order order) => Receipts.Describe(order, "Paid: ");
    }
}
