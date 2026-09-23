using System;
using Shop.Contracts;

namespace Shop
{
    public class Greeter : IGreeter
    {
        public string Greet(string name) => $"Hello {name}";

        public void Wave() => Console.WriteLine("o/");
    }
}
