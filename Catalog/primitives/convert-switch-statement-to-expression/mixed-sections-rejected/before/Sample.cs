using System;

namespace Shop
{
    public class Sample
    {
        public string Name(int code)
        {
            /*^*/switch (code)
            {
                case 1:
                    Console.WriteLine("one");
                    return "one";
                default:
                    return "other";
            }
        }
    }
}
