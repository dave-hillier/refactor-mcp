namespace Shop
{
    public class Order
    {
        private readonly int _quantity;
        private readonly double _itemPrice;

        public Order(int quantity, double itemPrice)
        {
            _quantity = quantity;
            _itemPrice = itemPrice;
        }

        public double Price()
        {
            double /*^*/basePrice = _quantity * _itemPrice;
            double discountFactor;
            if (basePrice > 1000)
                discountFactor = 0.95;
            else
                discountFactor = 0.98;
            return basePrice * discountFactor;
        }

        public int Quantity()
        {
            return _quantity;
        }
    }
}
