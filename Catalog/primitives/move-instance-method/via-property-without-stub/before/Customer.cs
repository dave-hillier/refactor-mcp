namespace Shop
{
    public class Customer
    {
        public Address Address { get; } = new Address();

        public string Name { get; set; }

        public string Label() => Name + ", " + Address.Town;

        public string Envelope() => "To: " + Label();
    }
}
