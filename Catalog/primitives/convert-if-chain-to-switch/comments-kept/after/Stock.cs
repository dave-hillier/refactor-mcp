using System;

namespace Shop
{
    public class Stock
    {
        public void Report(string code)
        {
            // Codes come from the warehouse.
            switch (code)
            {
                case "A":
                    // Plenty left.
                    Console.WriteLine("available");
                    break;
                case "B":
                    Console.WriteLine("low"); // reorder soon
                    break;
            }
        }
    }
}
