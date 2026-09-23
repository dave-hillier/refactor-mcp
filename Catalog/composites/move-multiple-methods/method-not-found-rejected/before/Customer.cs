namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Name { get; set; }

        public string Label() => Name + ", " + Street() + ", " + _address.Town;

        // The house number goes first.
        public string Street() => _address.Number + " " + _address.Road;
    }
}
