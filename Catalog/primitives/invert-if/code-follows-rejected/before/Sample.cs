using System;

namespace Shop
{
    public class Sample
    {
        public void Log(int value)
        {
            /*^*/if (value > 0)
            {
                Console.WriteLine("positive");
            }

            Console.WriteLine(value);
        }
    }
}
