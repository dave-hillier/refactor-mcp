using Shop.Payments;

namespace Shop
{
    public class Order
    {
        public Invoice Bill() => new Invoice();

        public Shop.Payments.Invoice Refund() => new Shop.Payments.Invoice();
    }
}
