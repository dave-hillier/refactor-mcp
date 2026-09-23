namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();
        private string _street = "", _town = "";

        public string Label() => _street + ", " + _town;
    }

    public class Address
    {
    }
}
