namespace Shop
{
    public class Product
    {
        public Product(string code)
        {
            Code = code;
        }

        public string Code { get; }
    }

    public class Catalogue
    {
        public string Find(Product product) => product.Code;
    }
}
