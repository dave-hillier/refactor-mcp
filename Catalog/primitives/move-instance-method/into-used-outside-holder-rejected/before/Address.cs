namespace Shop
{
    public class Address
    {
        public string Street { get; set; }

        public string Format() => Street;

        public string Shout() => Format().ToUpper();
    }

    public class Customer
    {
        private readonly Address _address = new Address();

        public string Label() => _address.Format();
    }
}
