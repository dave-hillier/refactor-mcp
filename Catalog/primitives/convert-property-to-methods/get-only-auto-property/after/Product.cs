namespace Shop
{
    public class Product
    {
        private readonly string _code;

        public Product(string code)
        {
            _code = code;
        }

        public string GetCode() => _code;
    }

    public class Catalogue
    {
        public string Find(Product product) => product.GetCode();
    }
}
