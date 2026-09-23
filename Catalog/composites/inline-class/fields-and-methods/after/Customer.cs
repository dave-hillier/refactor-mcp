namespace Shop
{
    public class Customer
    {
        private readonly string _country = "UK";

        public string Name { get; set; }

        public void MoveTo(string street, string city)
        {
            Street = street;
            this.City = city;
        }

        public string Label() => Name + "\n" + Format();

        public string Street { get; set; }

        public string City { get; set; }

        // One line, for labels.
        public string Format() => Street + ", " + City + ", " + _country;
    }
}
