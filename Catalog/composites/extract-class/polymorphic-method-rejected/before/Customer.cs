namespace Shop
{
    public class Customer
    {
        public string Name = "";

        private string _street = "";
        private string _town = "";

        public void MoveTo(string street, string town)
        {
            _street = street;
            _town = town;
        }

        public virtual string FormatAddress()
        {
            return _street + ", " + _town;
        }
    }
}
