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
        public string Greet(string name)
        {
            var clock = new Clock();
            return (/*^*/clock.Hour() < 12 ? "Good morning, " : "Hello, ") + name;
        }

        public static string Sample()
        {
            return new Greeter().Greet("Ann");
        }
    }
}
