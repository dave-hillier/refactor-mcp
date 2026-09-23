using System;

namespace Shop
{
    public class Config
    {
        // Seconds before a request is abandoned.
        private int _timeout = 30;

        public Config()
        {
            Console.WriteLine(_timeout);
        }

        public int Timeout() => _timeout;
    }
}
