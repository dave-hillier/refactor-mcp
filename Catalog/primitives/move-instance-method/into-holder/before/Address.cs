namespace Shop
{
    public class Address
    {
        private string _country = "UK";

        public string Street { get; set; }

        public string City { get; set; }

        // One line, for labels.
        public string Format()
        {
            return Street + ", " + City + ", " + this._country;
        }
    }
}
