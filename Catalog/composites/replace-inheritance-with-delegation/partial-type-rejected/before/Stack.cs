using System.Collections.Generic;

namespace Shop
{
    public partial class Stack : List<int>
    {
        public void Push(int value) => Add(value);
    }

    public partial class Stack
    {
        public int Peek() => this[Count - 1];
    }
}
