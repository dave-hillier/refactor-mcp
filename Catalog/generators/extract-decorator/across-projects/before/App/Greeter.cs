using Core;

namespace App
{
    public class Greeter : IGreeter
    {
        public string Greet(string name) => "Hello " + name;
    }
}
