using System.Collections.Generic;

namespace Shop
{
    public class Stack
    {
        private readonly List<int> _list = new List<int>();

        public void Push(int value) => _list.Add(value);
    }
}
