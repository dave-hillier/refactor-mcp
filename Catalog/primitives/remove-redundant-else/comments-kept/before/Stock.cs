using System;

namespace Shop
{
    public class Stock
    {
        public int Reorder(int level, int minimum)
        {
            // Nothing to do when stocked.
            /*^*/if (level >= minimum)
            {
                return 0; // stocked
            }
            // Below the minimum.
            else
            {
                // Order enough to reach it.
                var shortfall = minimum - level;
                Console.WriteLine(shortfall);

                return shortfall; // ordered
            }
        }
    }
}
