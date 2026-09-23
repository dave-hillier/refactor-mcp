using System.Collections.Generic;

namespace Shop
{
    // Last in, first out.
    public class Stack : List<int>
    {
        public void Push(int value) => Add(value);

        public int Pop()
        {
            var top = this[Count - 1];
            RemoveAt(Count - 1);
            return top;
        }
    }
}
