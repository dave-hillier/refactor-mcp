using System.Collections.Generic;

namespace Stacks
{
    public class Pile
    {
        private readonly List<string> _items = new List<string>();

        internal string Pop()
        {
            var top = _items[_items.Count - 1];
            _items.RemoveAt(_items.Count - 1);
            return top;
        }

        public void Push(string item)
        {
            _items.Add(item);
        }

        public string Juggle()
        {
            // Throw the top one away.
            this.Pop();
            string next;
            next = Pop();
            var last = Pop();
            return next + last;
        }
    }
}
