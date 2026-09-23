namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Label() => Label("");

        public string Label(string prefix) => prefix + _address.Town;
    }
}
