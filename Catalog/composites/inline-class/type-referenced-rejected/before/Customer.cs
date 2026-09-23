namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Label() => Describe(_address);

        private static string Describe(Address address) => address.Street;
    }
}
