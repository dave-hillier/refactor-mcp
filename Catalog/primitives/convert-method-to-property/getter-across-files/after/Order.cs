namespace Shop
{
    public class Order
    {
        private readonly decimal _price;
        private readonly int _quantity;

        public Order(decimal price, int quantity)
        {
            _price = price;
            _quantity = quantity;
        }

        public decimal Total => _price * _quantity;

        public bool IsLarge() => Total > 100;
    }
}
