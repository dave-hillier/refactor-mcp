using System.Collections.Generic;

namespace Shop
{
    public class Stack : List<int>
    {
        public int Peek() => this[Count - 1];
    }
}
