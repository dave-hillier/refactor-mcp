using System.Collections.Generic;

namespace Shop
{
    public class Cart
    {
        private readonly List<string> _items = new List<string>();

        public bool IsEmpty
        {
            get { return _items.Count == 0; }
        }

        public string Summary() => IsEmpty ? "empty" : _items.Count + " items";
    }
}
