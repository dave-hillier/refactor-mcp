using System;

namespace Shop
{
    public interface IGreeter
    {
        string Greet(string name);
    }

    public class Greeter : IGreeter, IDisposable
    {
        public string Greet(string name) => "Hello " + name;

        public void Dispose()
        {
        }
    }
}
