namespace Shop
{
    public class Address
    {
        public string city;
    }

    public class Customer
    {
        private readonly Address _address;

        public Customer(Address address)
        {
            _address = address;
        }

        public string City() => _address.city;
    }
}
