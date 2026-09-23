using System;

namespace Shop
{
    public class Sample
    {
        public void Scan(int[] values)
        {
            foreach (var value in values)
            {
                /*^*/if (value == 0)
                {
                    break;
                }
                else if (value == 1)
                {
                    Console.WriteLine("one");
                }
            }
        }
    }
}
