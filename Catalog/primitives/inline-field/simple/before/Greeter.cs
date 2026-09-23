namespace Shop
{
    public class Greeter
    {
        private readonly string _greeting = "Hello";
        private int _count;

        public string Greet(string name)
        {
            _count++;
            return this._greeting + ", " + name;
        }

        public int Count => _count;

        public bool IsGreeting(string text) => text == _greeting;
    }
}
