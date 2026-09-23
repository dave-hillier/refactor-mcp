namespace Shop
{
    public class Order
    {
        private readonly Customer _customer = new Customer();

        public decimal Total { get; set; }
    }
}
