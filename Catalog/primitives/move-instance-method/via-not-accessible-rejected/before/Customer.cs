namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Where() => _address.Town;
    }

    public class Address
    {
        public string Town { get; set; }
    }
}
