namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string? Postcode { get; set; }

        public void MoveTo(string street, string town)
        {
            _address._street = street;
            _address._town = town;
        }

        public string Label() => _address.FormatAddress() + " " + Postcode;
    }
}
