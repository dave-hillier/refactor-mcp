using System.Collections.Generic;

namespace Shop
{
    // Last in, first out.
    public class Stack : List<int>
    {
        private readonly List<int> _list = new List<int>();

        public new int Count => base.Count;

        public new int this[int index] { get => base[index]; set => base[index] = value; }

        public void Push(int value) => this.Add(value); // on top

        public int Pop()
        {
            // The last element is the top.
            var top = base[base.Count - 1];
            base.RemoveAt(base.Count - 1);
            return top;
        }
    }
}
