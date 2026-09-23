using System.Collections.Generic;

namespace Shop
{
    public class Stack : List<int>
    {
        private readonly List<int> _list = new List<int>();

        public void Push(int value) => _list.Add(value);

        public int Pop()
        {
            var top = _list[_list.Count - 1];
            _list.RemoveAt(_list.Count - 1);
            return top;
        }
    }
}
