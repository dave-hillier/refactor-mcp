namespace Shop
{
    public class Customer
    {
        public string Name = "";

        // Where letters go.
        private string _street = "";
        private string _town = "";

        public void MoveTo(string street, string town)
        {
            _street = street;
            _town = town;
        }

        public string FormatAddress()
        {
            return _street + ", " + _town;
        }
    }
}
