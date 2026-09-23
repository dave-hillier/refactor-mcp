using Shop.Contracts;

namespace Shop
{
    public static class Program
    {
        public static string Run()
        {
            IGreeter greeter = new Greeter();
            return greeter.Greet("Ada");
        }
    }
}
