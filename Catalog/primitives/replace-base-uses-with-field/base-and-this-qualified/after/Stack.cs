using System.Collections.Generic;

namespace Shop
{
    // Last in, first out.
    public class Stack : List<int>
    {
        private readonly List<int> _list = new List<int>();

        public new int Count => _list.Count;

        public new int this[int index] { get => _list[index]; set => _list[index] = value; }

        public void Push(int value) => _list.Add(value); // on top

        public int Pop()
        {
            // The last element is the top.
            var top = _list[_list.Count - 1];
            _list.RemoveAt(_list.Count - 1);
            return top;
        }
    }
}
