namespace Shop
{
    public class Address
    {
        public string Street { get; set; }
    }

    public class Customer
    {
        private readonly Address _home = new Address();
        private readonly Address _work = new Address();

        public string Streets() => _home.Street + " / " + _work.Street;
    }
}
