namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Country() => _address._country;
    }
}
