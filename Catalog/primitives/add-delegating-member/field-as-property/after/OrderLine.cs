namespace Shop
{
    public class OrderLine
    {
        private readonly Product _product = new Product();
        private int _quantity = 1;

        public string Sku => _product.Sku;

        public decimal Total() => _product.Price * _quantity;
    }
}
