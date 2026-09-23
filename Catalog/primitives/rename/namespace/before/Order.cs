using Shop.Billing;

namespace Shop
{
    public class Order
    {
        public Invoice Bill() => new Invoice();

        public Shop.Billing.Invoice Refund() => new Shop.Billing.Invoice();
    }
}
