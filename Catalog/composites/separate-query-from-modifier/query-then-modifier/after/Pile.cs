using System.Collections.Generic;

namespace Stacks
{
    public class Pile
    {
        private readonly List<string> _items = new List<string>();

        internal string Peek()
        {
            return _items[_items.Count - 1];
        }

        internal void RemoveTop()
        {
            _items.RemoveAt(_items.Count - 1);
        }

        public void Push(string item)
        {
            _items.Add(item);
        }

        public string Juggle()
        {
            // Throw the top one away.
            this.RemoveTop();
            string next;
            next = Peek();
            RemoveTop();
            var last = Peek();
            RemoveTop();
            return next + last;
        }
    }
}
