using System.Collections.Generic;

namespace Shop
{
    public class Cart
    {
        private readonly List<string> _items = new List<string>();

        public int Count => _items.Count;
    }

    public class Checkout
    {
        public bool CanPay(Cart cart) => cart.Count > 0;
    }
}
