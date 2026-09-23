namespace Shop
{
    public class Address
    {
        public string Street { get; set; }

        public string Format() => Street;
    }

    public class Customer
    {
        private readonly Address _address = new Address();

        public string Format() => "Customer at " + _address.Format();
    }
}
