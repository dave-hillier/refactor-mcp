namespace Shop
{
    public class Customer
    {
        private readonly Address _home = new Address();

        public string Name { get; set; }

        public string Where() => _home.Where();
    }

    public class Address
    {
        public string Town { get; set; }

        public string Where() => Town.ToUpperInvariant();
    }
}
