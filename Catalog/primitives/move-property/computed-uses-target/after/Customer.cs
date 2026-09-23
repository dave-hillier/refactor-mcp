namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Label() => "Town: " + _address.Town;
    }

    public class Address
    {
        public string City { get; set; }

        public string Town => City.ToUpperInvariant();
    }
}
