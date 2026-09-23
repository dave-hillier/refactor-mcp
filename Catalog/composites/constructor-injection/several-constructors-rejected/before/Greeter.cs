namespace Shop
{
    public class Clock
    {
        public int Hour()
        {
            return 12;
        }
    }

    public class Greeter
    {
        private readonly string _greeting;

        public Greeter()
        {
            _greeting = "Hello";
        }

        public Greeter(string greeting)
        {
            _greeting = greeting;
        }

        public string Greet(string name)
        {
            var /*^*/clock = new Clock();
            return clock.Hour() + _greeting + name;
        }
    }
}
