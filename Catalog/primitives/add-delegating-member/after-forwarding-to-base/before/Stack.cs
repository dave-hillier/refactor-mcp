using System.Collections.Generic;

namespace Shop
{
    // Last in, first out.
    public class Stack : List<int>
    {
        private readonly List<int> _list = new List<int>();

        public new int Count => base.Count;

        public void Push(int value) => Add(value);

        public int Pop()
        {
            var top = this[base.Count - 1];
            RemoveAt(base.Count - 1);
            return top;
        }
    }
}
