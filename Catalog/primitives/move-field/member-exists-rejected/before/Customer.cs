namespace Shop
{
    public class Customer
    {
        public Address Address { get; } = new Address();

        public string Town;
    }

    public class Address
    {
        public string Town { get; set; }
    }
}
