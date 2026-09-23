namespace Shop
{
    public class Address
    {
        // Where letters go.
        internal string _street = "";

        internal string _town = "";

        public string FormatAddress()
        {
            return _street + ", " + _town;
        }
    }
}
