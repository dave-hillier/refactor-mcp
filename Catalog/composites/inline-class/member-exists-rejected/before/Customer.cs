namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Street { get; set; }

        public string Label() => Street + " / " + _address.Street;
    }
}
