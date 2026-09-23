namespace Shop
{
    public class Customer
    {
        public Address Address { get; } = new Address();

        public string Full => Address.Number + " " + Address.Street;
    }
}
