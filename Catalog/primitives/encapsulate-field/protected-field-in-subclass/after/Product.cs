namespace Shop
{
    public class Product
    {
        private string _title = "";
        private decimal _price;

        protected string DisplayTitle
        {
            get => _title;
            set => _title = value;
        }

        public decimal Price() => _price;

        public string Label() => _title.ToUpperInvariant();
    }
}
