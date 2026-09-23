namespace Shop
{
    public class OrderLine
    {
        private readonly Product _product = new Product();
        private int _quantity = 1;

        public decimal Total() => _product.Price * _quantity;
    }
}
