using System.Collections.Generic;

namespace Shop
{
    public class Stack : List<int>
    {
        private readonly List<int> _list = new List<int>();

        public void Push(int value) => Add(value);

        public void Log(int value) => _list.Add(value);
    }
}
