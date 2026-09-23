namespace Shop
{
    public class Product
    {
        private readonly string _code;

        public string Code => _code;

        public Product(string code)
        {
            _code = code;
        }
    }
}
