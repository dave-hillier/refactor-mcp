namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        // The first line of the address.
        private string _street = "";

        public void Move(string street) => _street = street;

        public string Label() => "Lives on " + this._street;
    }
}
