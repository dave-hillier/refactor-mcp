namespace Shop
{
    public class Order
    {
        private readonly Customer _customer = new Customer();

        public string Greeting(string salutation) => _customer.Greeting(salutation);

        public decimal Total { get; set; }
    }
}
