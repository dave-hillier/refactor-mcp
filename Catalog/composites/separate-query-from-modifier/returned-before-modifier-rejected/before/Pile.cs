using System.Collections.Generic;

namespace Stacks
{
    public class Pile
    {
        private readonly List<string> _items = new List<string>();

        public string Pop()
        {
            var top = _items[_items.Count - 1];
            _items.RemoveAt(_items.Count - 1);
            return top;
        }

        public string Discard()
        {
            return Pop();
        }
    }
}
