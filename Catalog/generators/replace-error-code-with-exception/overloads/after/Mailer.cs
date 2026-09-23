using System;

namespace Shop
{
    public class Mailer
    {
        public void Send(string message)
        {
            if (message.Length == 0)
                throw new InvalidOperationException("Send returned error code 1");
        }

        public int Send(string message, int retries)
        {
            if (retries < 0)
                return 2;
            return 0;
        }

        public void Notify()
        {
            try
            {
                Send("hi");
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("failed");
            }
            if (Send("hi", 3) != 0)
                Console.WriteLine("failed again");
        }
    }
}
