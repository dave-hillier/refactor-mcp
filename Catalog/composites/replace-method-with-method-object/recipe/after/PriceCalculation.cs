namespace Shop
{
    public class PriceCalculation
    {
        private decimal basePrice;
        private decimal discount;

        /// <summary>The price of a line of this order.</summary>
        public decimal Price(Order order, int quantity, decimal itemPrice)
        {
            // Large orders earn the discount.
            basePrice = quantity * itemPrice;
            discount = basePrice > 1000 ? basePrice * order._discountRate : 0;
            return basePrice - discount;
        }
    }
}
