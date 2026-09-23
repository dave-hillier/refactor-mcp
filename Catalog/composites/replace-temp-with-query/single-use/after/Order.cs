using System;

namespace Shop
{
    public class Order
    {
        private readonly int _quantity;
        private readonly decimal _itemPrice;

        public Order(int quantity, decimal itemPrice)
        {
            _quantity = quantity;
            _itemPrice = itemPrice;
        }

        public decimal Price()
        {
            decimal shipping = Math.Min(_quantity * 2m, 50m);
            return BasePrice() + shipping;
        }

        private decimal BasePrice()
        {
            return _quantity * _itemPrice;
        }
    }
}
