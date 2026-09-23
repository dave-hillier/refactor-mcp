using System;
using System.Collections.Generic;

namespace Shop
{
    public class Batch
    {
        public void Process(List<int> quantities)
        {
            foreach (var quantity in quantities)
            {
                if (quantity <= 0)
                {
                    continue;
                }

                Console.WriteLine(quantity);
            }
        }
    }
}
