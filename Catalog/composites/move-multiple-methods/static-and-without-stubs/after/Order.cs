namespace Shop
{
    public class Order
    {
        private readonly Pricing _pricing = new Pricing();

        public int Quantity { get; set; }

        public decimal Total() => Quantity * _pricing.UnitPrice();
    }
}
