namespace Shop
{
    public class Order
    {
        public Invoice Invoice { get; set; }
    }

    /// <summary>What the customer is asked to pay.</summary>
    public class Invoice
    {
        public decimal Amount { get; set; }
    }

    public class Customer
    {
        public string Name { get; set; }
    }
}
