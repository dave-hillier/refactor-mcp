namespace Shop
{
    public class PriceCalculation
    {
        private readonly Order _order;
        private readonly int _quantity;
        private readonly decimal _itemPrice;
        private decimal _basePrice;
        private decimal _discount;

        public PriceCalculation(Order order, int quantity, decimal itemPrice)
        {
            _order = order;
            _quantity = quantity;
            _itemPrice = itemPrice;
        }

        public decimal Compute()
        {
            // Large orders earn the discount.
            _basePrice = _quantity * _itemPrice;
            _discount = _basePrice > 1000 ? _basePrice * _order._discountRate : 0;
            return _basePrice - _discount;
        }
    }
}
