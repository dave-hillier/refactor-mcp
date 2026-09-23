namespace Shop
{
    public class Customer
    {
        private Address _address = new Address();

        public void Reset() => _address = new Address();
    }

    public class Address
    {
    }
}
