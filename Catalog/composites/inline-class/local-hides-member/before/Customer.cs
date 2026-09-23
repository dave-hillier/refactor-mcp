namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public void Update(string city)
        {
            _address.city = city;
        }
    }

    public class Address
    {
        public string city;
    }
}
