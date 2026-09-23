namespace Shop
{
    public class Product
    {
        protected string _title = "";
        private decimal _price;

        public decimal Price() => _price;

        public string Label() => _title.ToUpperInvariant();
    }
}
