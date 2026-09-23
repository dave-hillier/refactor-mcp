using System.Collections.Generic;

namespace Shop
{
    public class Stack : List<int>
    {
        public new int this[int index] { get => base[index]; set => base[index] = value; }

        public void Push(int value) => Add(value);

        public int Pop()
        {
            var top = base[Count - 1];
            RemoveAt(this.Count - 1);
            return top;
        }
    }
}
