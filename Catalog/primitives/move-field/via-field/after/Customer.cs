namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public void Move(string street) => _address._street = street;

        public string Label() => "Lives on " + this._address._street;
    }
}
