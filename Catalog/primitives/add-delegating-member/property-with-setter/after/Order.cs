namespace Shop
{
    public class Order
    {
        private readonly Customer _customer = new Customer();

        public string Name { get => _customer.Name; set => _customer.Name = value; }

        public decimal Total { get; set; }
    }
}
