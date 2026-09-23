using System.Collections.Generic;

namespace Shop
{
    public class Stack
    {
        private readonly List<int> _list = new List<int>();

        public int Count => _list.Count;

        public int this[int index] { get => _list[index]; set => _list[index] = value; }

        public void Push(int value) => _list.Add(value);
    }
}
