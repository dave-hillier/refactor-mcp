namespace Shop
{
    public class Customer
    {
        public Address Address { get; } = new Address();

        public string Street { get; set; }

        public static Customer Create() => new Customer { Street = "High Street" };
    }

    public class Address
    {
    }
}
