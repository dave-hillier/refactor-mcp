using System.Collections.Generic;

namespace Shop
{
    public class Basket
    {
        private readonly List<string> _items = new List<string>();

        public void Add(string item) => _items.Add(item);

        public int Count => _items.Count;
    }
}
