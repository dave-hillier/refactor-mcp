namespace Shop
{
    public partial class Product
    {
        private decimal _price;

        public string Code { get; }

        public decimal Price() => _price;
    }
}
