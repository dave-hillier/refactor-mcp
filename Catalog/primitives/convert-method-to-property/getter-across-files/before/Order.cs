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

        public decimal GetTotal()
        {
            return _price * _quantity;
        }

        public bool IsLarge() => GetTotal() > 100;
    }
}
