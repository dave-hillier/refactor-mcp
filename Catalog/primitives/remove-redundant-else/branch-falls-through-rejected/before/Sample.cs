using System;

namespace Shop
{
    public class Sample
    {
        public void Report(int value)
        {
            /*^*/if (value < 0)
            {
                Console.WriteLine("negative");
                if (value < -100)
                {
                    return;
                }
            }
            else
            {
                Console.WriteLine("positive");
            }
        }
    }
}
