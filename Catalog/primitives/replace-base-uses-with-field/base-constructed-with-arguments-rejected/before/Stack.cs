using System.Collections.Generic;

namespace Shop
{
    public class Stack : List<int>
    {
        private readonly List<int> _list = new List<int>();

        public Stack(IEnumerable<int> values) : base(values)
        {
        }

        public void Push(int value) => Add(value);
    }
}
