using System.Collections.Generic;

namespace Shop
{
    public class Inventory : List<string>
    {
        private readonly List<string> _items = new List<string>();

        public string Receive(string item)
        {
            _items.Add(item);
            return _items[0] + " of " + _items.Count;
        }
    }
}
