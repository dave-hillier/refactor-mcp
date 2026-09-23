namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Name { get; set; }

        public void MoveTo(string street, string city)
        {
            _address.Street = street;
            this._address.City = city;
        }

        public string Label() => Name + "\n" + _address.Format();
    }
}
