using System;

namespace Monitoring
{
    public class Alerts
    {
        public void Check(int cpu, int memory)
        {
            /*^*/if (cpu > 90)
            {
                Console.WriteLine("alert");
            }

            if (memory > 90)
            {
                Console.WriteLine("alert");
            }
        }
    }
}
