namespace Shop
{
    public class Customer
    {
        public Address Address { get; } = new Address();

        public string Postcode;

        public static Customer Create() => new Customer { Postcode = "AB1" };
    }

    public class Address
    {
    }
}
