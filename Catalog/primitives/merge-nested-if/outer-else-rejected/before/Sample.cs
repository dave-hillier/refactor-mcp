using System;

namespace Shop
{
    public class Sample
    {
        public void Log(bool a, bool b)
        {
            /*^*/if (a)
            {
                if (b)
                {
                    Console.WriteLine("both");
                }
            }
            else
            {
                Console.WriteLine("not a");
            }
        }
    }
}
