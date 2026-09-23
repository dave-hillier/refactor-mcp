namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();
        private string _street = "";

        public string Label() => _street + ", " + _address._town;
    }

    public class Address
    {
        internal string _town = "";
    }
}
