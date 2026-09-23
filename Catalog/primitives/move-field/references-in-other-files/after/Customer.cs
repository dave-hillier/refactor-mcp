namespace Shop
{
    public class Customer
    {
        public Address Address { get; } = new Address();
    }

    public class Address
    {
        public string Town;

        public string Postcode;
    }
}
