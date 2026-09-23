using System;

namespace Monitoring
{
    public class Alerts
    {
        public void Check(int load, int errors)
        {
            /*^*/if (load > 90)
            {
                Console.WriteLine("alert");
            }

            if (errors > 0)
            {
                Console.WriteLine("alert");
            }
        }
    }
}
