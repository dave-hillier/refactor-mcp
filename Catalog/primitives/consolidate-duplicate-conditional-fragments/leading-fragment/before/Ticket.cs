using System.Collections.Generic;

namespace Support
{
    public class Ticket
    {
        public string Describe(int code, List<string> log)
        {
            string label;
            /*^*/if (code == 1)
            {
                log.Add("describing");
                label = "open";
            }
            else if (code == 2)
            {
                log.Add("describing");
                label = "pending";
            }
            else
            {
                log.Add("describing");
                label = "closed";
            }

            return label;
        }
    }
}
