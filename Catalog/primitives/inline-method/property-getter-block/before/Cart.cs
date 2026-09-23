using System.Collections.Generic;

namespace Shop
{
    public class Cart
    {
        private readonly List<string> _items = new List<string>();

        // Whether anything has been added.
        public bool IsEmpty
        {
            get { return _items.Count == 0; }
        }

        public string Summary()
        {
            if (!IsEmpty)
            {
                return _items.Count + " items";
            }

            return "empty";
        }
    }
}
