namespace Shop
{
    public class Customer
    {
        private string _name = "";

        public string Shout() => _name.ToUpperInvariant();
    }
}
