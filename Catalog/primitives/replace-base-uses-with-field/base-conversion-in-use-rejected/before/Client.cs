using System.Collections.Generic;

namespace Shop
{
    public class Client
    {
        public int Run()
        {
            var stack = new Stack();
            stack.Push(1);
            IEnumerable<int> values = stack;
            return stack.Pop();
        }
    }
}
