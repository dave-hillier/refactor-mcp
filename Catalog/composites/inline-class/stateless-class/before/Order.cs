namespace Shop
{
    public class Order
    {
        private readonly PriceCalculator _calculator = new PriceCalculator();

        public decimal Total(decimal subtotal)
        {
            return _calculator.Discounted(subtotal);
        }
    }
}
