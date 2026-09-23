using System.Collections.Generic;

namespace Shop
{
    public class Cart
    {
        private readonly List<string> _items = new List<string>();

        public string Summary()
        {
            if (!(_items.Count == 0))
            {
                return _items.Count + " items";
            }

            return "empty";
        }
    }
}
