namespace Shop
{
    public class Customer
    {
        private readonly Address _home = new Address();
        private readonly Address _work = new Address();

        public string Towns() => _home.Town + _work.Town;
    }

    public class Address
    {
        public string Town { get; set; }
    }
}
