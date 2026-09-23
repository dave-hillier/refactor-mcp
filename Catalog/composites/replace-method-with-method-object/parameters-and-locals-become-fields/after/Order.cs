namespace Shop
{
    public class Order
    {
        internal decimal _discountRate = 0.1m;

        /// <summary>The price of a line of this order.</summary>
        public decimal Price(int quantity, decimal itemPrice)
        {
            return new PriceCalculation(this, quantity, itemPrice).Compute();
        }
    }
}
