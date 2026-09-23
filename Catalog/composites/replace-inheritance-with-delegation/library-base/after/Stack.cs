using System.Collections.Generic;

namespace Shop
{
    // Last in, first out.
    public class Stack
    {
        private readonly List<int> _list = new List<int>();

        public int Count => _list.Count;

        public int this[int index] { get => _list[index]; set => _list[index] = value; }

        public void Push(int value) => _list.Add(value);

        public int Pop()
        {
            var top = _list[_list.Count - 1];
            _list.RemoveAt(_list.Count - 1);
            return top;
        }
    }
}
