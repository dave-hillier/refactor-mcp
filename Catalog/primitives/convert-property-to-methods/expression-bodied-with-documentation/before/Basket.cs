namespace Shop
{
    public class Basket
    {
        private decimal _price;

        public void Add(decimal price) => _price += price;

        /// <summary>The price with tax.</summary>
        public decimal Total => _price * 1.2m;

        public string Describe() => "Total: " + this.Total;
    }
}
