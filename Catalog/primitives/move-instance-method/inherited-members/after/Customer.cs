namespace Shop
{
    public class Party
    {
        public string Name { get; set; }
    }

    public class Customer : Party
    {
        private readonly Address _address = new Address();

        public string Greeting() => _address.Greeting(this);
    }

    public class Address
    {
        public string Town { get; set; }

        public string Greeting(Customer customer) => "Dear " + customer.Name + " of " + Town;
    }
}
