namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Postcode;

        public Address Home() => _address;
    }

    public class Address
    {
    }

    public class Courier
    {
        public string Route(Customer customer) => customer.Postcode;
    }
}
