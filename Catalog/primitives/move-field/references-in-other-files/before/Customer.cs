namespace Shop
{
    public class Customer
    {
        public Address Address { get; } = new Address();

        public string Postcode;
    }

    public class Address
    {
        public string Town;
    }
}
