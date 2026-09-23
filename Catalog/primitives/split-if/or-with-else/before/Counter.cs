using System;

namespace Shop
{
    public class Counter
    {
        private int _count;

        public void Record(bool urgent, bool important)
        {
            /*^*/if (urgent || important)
            {
                _count++;
            }
            else
            {
                Console.WriteLine("ignored");
            }
        }
    }
}
