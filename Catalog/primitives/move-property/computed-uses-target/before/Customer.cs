namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Town => _address.City.ToUpperInvariant();

        public string Label() => "Town: " + Town;
    }

    public class Address
    {
        public string City { get; set; }
    }
}
