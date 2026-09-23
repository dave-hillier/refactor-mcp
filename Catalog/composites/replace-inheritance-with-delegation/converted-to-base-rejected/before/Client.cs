using System.Collections.Generic;

namespace Shop
{
    public class Client
    {
        public int Run()
        {
            var stack = new Stack();
            stack.Push(1);
            return Total(stack);
        }

        private static int Total(List<int> numbers) => numbers.Count;
    }
}
