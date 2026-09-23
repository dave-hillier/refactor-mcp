namespace Shop
{
    public class Customer
    {
        public Address Address { get; } = new Address();

        public string Name { get; set; }

        public string Envelope() => "To: " + Address.Label(this);
    }
}
