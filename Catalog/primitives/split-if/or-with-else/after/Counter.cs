using System;

namespace Shop
{
    public class Counter
    {
        private int _count;

        public void Record(bool urgent, bool important)
        {
            if (urgent)
            {
                _count++;
            }
            else if (important)
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
