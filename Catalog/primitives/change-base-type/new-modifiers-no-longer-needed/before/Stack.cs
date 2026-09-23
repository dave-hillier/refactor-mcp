using System.Collections.Generic;

namespace Shop
{
    public class Stack : List<int>
    {
        private readonly List<int> _list = new List<int>();

        public new int Count => _list.Count;

        public new int this[int index] { get => _list[index]; set => _list[index] = value; }

        public void Push(int value) => _list.Add(value);
    }
}
