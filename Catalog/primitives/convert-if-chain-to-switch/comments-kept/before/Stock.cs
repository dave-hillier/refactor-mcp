using System;

namespace Shop
{
    public class Stock
    {
        public void Report(string code)
        {
            // Codes come from the warehouse.
            /*^*/if (code == "A")
            {
                // Plenty left.
                Console.WriteLine("available");
            }
            else if (code == "B")
            {
                Console.WriteLine("low"); // reorder soon
            }
        }
    }
}
