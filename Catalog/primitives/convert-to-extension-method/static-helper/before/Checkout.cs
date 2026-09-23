namespace Shop
{
    public class Checkout
    {
        public string Welcome(Customer customer) => Text.Shout(customer.Name);
    }

    public class Customer
    {
        public string Name { get; set; }
    }
}
