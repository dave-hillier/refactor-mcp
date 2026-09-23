using System.Collections.Generic;

namespace Shop
{
    public class Stack : List<int>
    {
        private readonly List<long> _list = new List<long>();

        public void Push(int value) => Add(value);

        public int Pop()
        {
            var top = this[Count - 1];
            RemoveAt(Count - 1);
            return top;
        }
    }
}
