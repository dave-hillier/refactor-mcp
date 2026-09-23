using System;

namespace Shop
{
    public class Mailer
    {
        public int Send(string message)
        {
            if (message.Length == 0)
                return 1;
            return 0;
        }

        public int Send(string message, int retries)
        {
            if (retries < 0)
                return 2;
            return 0;
        }

        public void Notify()
        {
            if (Send("hi") != 0)
                Console.WriteLine("failed");
            if (Send("hi", 3) != 0)
                Console.WriteLine("failed again");
        }
    }
}
