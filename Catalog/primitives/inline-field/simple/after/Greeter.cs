namespace Shop
{
    public class Greeter
    {
        private int _count;

        public string Greet(string name)
        {
            _count++;
            return "Hello" + ", " + name;
        }

        public int Count => _count;

        public bool IsGreeting(string text) => text == "Hello";
    }
}
