using System;

namespace Metrics
{
    public class Counter
    {
        private int _count;

        public void Record()
        {
            /*^*/if (_count > 10)
            {
                _count++;
                Console.WriteLine("many");
            }
            else
            {
                _count++;
                Console.WriteLine("few");
            }
        }
    }
}
