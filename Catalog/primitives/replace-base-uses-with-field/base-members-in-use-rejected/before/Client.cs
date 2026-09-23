namespace Shop
{
    public class Client
    {
        public int Run()
        {
            var stack = new Stack();
            stack.Push(1);
            return stack.Count;
        }
    }
}
