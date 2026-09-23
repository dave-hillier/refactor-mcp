namespace Shop
{
    public partial class Product
    {
        private decimal _price;
        private readonly string _code;

        public string Code => _code;

        public decimal Price() => _price;
    }
}
