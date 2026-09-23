namespace Shop
{
    public partial class Product
    {
        public Product(string code, decimal price)
        {
            _code = code;
            _price = price;
        }

        public string Describe() => Code + ": " + _price;
    }
}
