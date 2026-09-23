namespace Shop
{
    public class Client
    {
        public int Run()
        {
            var stack = new Stack();
            stack.Push(1);
            stack[0] = 5;
            return stack.Pop() + stack.Count;
        }
    }
}
