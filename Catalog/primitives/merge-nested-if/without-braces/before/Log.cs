using System;

namespace Shop
{
    public class Log
    {
        public void Write(string message, bool verbose)
        {
            /*^*/if (verbose)
                if (message.Length > 0)
                    Console.WriteLine(message);
        }
    }
}
