using System;
using System.Collections.Generic;

namespace Shop
{
    public class Batch
    {
        public void Process(List<int> quantities, int mode)
        {
            foreach (var quantity in quantities)
            {
                switch (mode)
                {
                    case 1:
                        if (quantity == 0)
                        {
                            continue;
                        }

                        Console.WriteLine(quantity);

                        break;
                    default:
                        Console.WriteLine(mode);
                        break;
                }
            }
        }
    }
}
