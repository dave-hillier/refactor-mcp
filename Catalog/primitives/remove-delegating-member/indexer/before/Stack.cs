using System.Collections.Generic;

namespace Shop
{
    public class Stack : List<int>
    {
        public new int this[int index] { get => base[index]; set => base[index] = value; }

        public int Peek() => base[Count - 1];
    }
}
