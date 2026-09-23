using System;

namespace Shop
{
    public class Greeter
    {
        public string Greet(string name, string? title)
        {
            return title is null ? "Hello " + name : "Hello " + title + " " + name;
        }
    }
}
