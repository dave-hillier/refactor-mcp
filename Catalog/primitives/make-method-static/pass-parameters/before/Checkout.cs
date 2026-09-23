namespace Shop
{
    public class Checkout
    {
        public string Receipt(Order order) => order.Describe("Paid: ");
    }
}
