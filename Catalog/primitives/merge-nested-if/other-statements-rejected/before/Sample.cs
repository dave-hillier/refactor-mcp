using System;

namespace Shop
{
    public class Sample
    {
        public void Log(bool a, bool b)
        {
            /*^*/if (a)
            {
                Console.WriteLine("a");
                if (b)
                {
                    Console.WriteLine("both");
                }
            }
        }
    }
}
