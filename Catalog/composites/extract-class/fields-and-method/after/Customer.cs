namespace Shop
{
    public class Customer
    {
        public string Name = "";
        private readonly Address _address = new Address();

        public void MoveTo(string street, string town)
        {
            _address._street = street;
            _address._town = town;
        }

        public string FormatAddress()
        {
            return _address.FormatAddress();
        }
    }
}
