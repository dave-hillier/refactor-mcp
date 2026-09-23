using System;

namespace Shop
{
    public class Sample
    {
        public void Log(string message, bool /*^*/verbose)
        {
            if (verbose)
            {
                Console.WriteLine(message);
            }
        }
    }
}
