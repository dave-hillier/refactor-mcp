using System;

namespace Shop
{
    public class Sample
    {
        public int Log(int value)
        {
            if (value != 0)
            {
                /*^*/if (value > 0)
                {
                    Console.WriteLine("positive");
                }
            }

            return value;
        }
    }
}
