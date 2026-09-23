namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Name { get; set; }

        public string Label() => _address.Label(this);

        public string Street() => _address.Street();
    }
}
