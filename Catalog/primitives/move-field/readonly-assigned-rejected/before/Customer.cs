namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();
        private readonly string _street;

        public Customer(string street) => _street = street;

        public string Street() => _street;
    }

    public class Address
    {
    }
}
