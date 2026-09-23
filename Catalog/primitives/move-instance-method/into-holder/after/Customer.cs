namespace Shop
{
    public class Customer
    {
        public Address Address { get; } = new Address();

        public string Label() => Format();

        // One line, for labels.
        public string Format()
        {
            return Address.Street + ", " + Address.City + ", " + Address._country;
        }
    }
}
