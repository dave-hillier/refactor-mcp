namespace Shop
{
    public class Order
    {
        private decimal _discountRate = 0.1m;

        /// <summary>The price of a line of this order.</summary>
        public decimal Price(int quantity, decimal itemPrice)
        {
            // Large orders earn the discount.
            decimal basePrice = quantity * itemPrice;
            decimal discount = basePrice > 1000 ? basePrice * _discountRate : 0;
            return basePrice - discount;
        }
    }
}
