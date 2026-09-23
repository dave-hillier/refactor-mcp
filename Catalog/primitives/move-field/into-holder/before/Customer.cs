namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Name { get; set; }

        public void MoveTo(string city)
        {
            _address.city = city;
        }

        public string Label() => Name + ", " + this._address.city;
    }
}
