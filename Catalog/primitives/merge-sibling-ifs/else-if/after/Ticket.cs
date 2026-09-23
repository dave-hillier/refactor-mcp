using System;

namespace Support
{
    public class Ticket
    {
        public void Route(int priority, bool vip)
        {
            if (priority > 3 || vip)
            {
                Console.WriteLine("urgent");
            }
            else
            {
                Console.WriteLine("queued");
            }
        }
    }
}
