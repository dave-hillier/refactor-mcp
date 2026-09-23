using System;

namespace Shop
{
    public class Greeter
    {
        public string Greet(string name, string? title)
        {
            ArgumentNullException.ThrowIfNull(name);

            return title is null ? "Hello " + name : "Hello " + title + " " + name;
        }
    }
}
