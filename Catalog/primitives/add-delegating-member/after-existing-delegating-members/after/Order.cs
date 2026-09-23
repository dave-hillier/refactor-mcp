namespace Shop
{
    public class Order
    {
        // Who placed the order.
        private readonly Customer _customer = new Customer();

        // Forwarded so callers need not reach the customer.
        public string Name => _customer.Name;

        public string Greeting(string salutation) => _customer.Greeting(salutation);

        /// <summary>The order's total.</summary>
        public decimal Total { get; set; }
    }
}
