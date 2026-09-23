namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Label() => _address.Street;
    }
}
