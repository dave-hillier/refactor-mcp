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
            decimal /*^*/basePrice = _quantity * _itemPrice;
            decimal shipping = Math.Min(_quantity * 2m, 50m);
            return basePrice + shipping;
        }
    }
}
