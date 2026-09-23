using Shop.Billing;

namespace Shop
{
    public class Order
    {
        public Invoice Invoice { get; set; }
    }

    public class Customer
    {
        public string Name { get; set; }
    }
}

namespace Shop.Billing
{
    /// <summary>What the customer is asked to pay.</summary>
    public class Invoice
    {
        public decimal Amount { get; set; }
    }
}
