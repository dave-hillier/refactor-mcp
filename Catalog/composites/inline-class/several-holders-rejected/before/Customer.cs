namespace Shop
{
    public class Customer
    {
        private readonly Address _home = new Address();
        private readonly Address _work = new Address();

        public string Label() => _home.Street + " / " + _work.Street;
    }
}
