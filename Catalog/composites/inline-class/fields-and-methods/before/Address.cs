namespace Shop
{
    /// <summary>Where a customer lives.</summary>
    public class Address
    {
        private readonly string _country = "UK";

        public string Street { get; set; }

        public string City { get; set; }

        // One line, for labels.
        public string Format() => Street + ", " + City + ", " + _country;
    }
}
