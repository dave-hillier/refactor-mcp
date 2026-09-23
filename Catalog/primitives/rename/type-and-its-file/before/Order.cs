namespace Shop
{
    public class Order
    {
        private readonly Customer _customer = new Customer("Ada");

        public Customer Buyer => _customer;
    }
}
