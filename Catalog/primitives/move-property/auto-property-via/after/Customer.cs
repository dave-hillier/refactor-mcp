namespace Shop
{
    public class Customer
    {
        public Address Address { get; } = new Address();

        public string Label() => Address.Street + ", " + Address.Town;
    }
}
