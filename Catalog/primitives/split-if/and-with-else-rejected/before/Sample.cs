using System;

namespace Shop
{
    public class Sample
    {
        public void Log(bool a, bool b)
        {
            /*^*/if (a && b)
            {
                Console.WriteLine("both");
            }
            else
            {
                Console.WriteLine("not both");
            }
        }
    }
}
