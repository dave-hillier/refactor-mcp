using System;

namespace Shop
{
    public class Log
    {
        public void Write(string message, bool verbose)
        {
            if (verbose && message.Length > 0)
                Console.WriteLine(message);
        }
    }
}
