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
        private readonly Clock _time;

        public Greeter(Clock time)
        {
            _time = time;
        }

        public string Greet(string name)
        {
            return (_time.Hour() < 12 ? "Good morning, " : "Hello, ") + name;
        }

        public static string Sample()
        {
            return new Greeter(new Clock()).Greet("Ann");
        }
    }
}
