namespace Shop
{
    public class Checkout
    {
        public string Welcome(Customer customer) => customer.Name.Shout();
    }

    public class Customer
    {
        public string Name { get; set; }
    }
}
