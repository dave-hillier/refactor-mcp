namespace Shop
{
    public class Address
    {
        private readonly string _country = "UK";

        // One line, for labels.
        public string Format() => Street + ", " + City + ", " + _country;

        public string Street { get; set; }

        public string City { get; set; }
    }
}
