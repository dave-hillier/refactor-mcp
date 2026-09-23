using System.Collections.Generic;

namespace Support
{
    public class Ticket
    {
        public string Describe(int code, List<string> log)
        {
            string label;
            log.Add("describing");

            if (code == 1)
            {
                label = "open";
            }
            else if (code == 2)
            {
                label = "pending";
            }
            else
            {
                label = "closed";
            }

            return label;
        }
    }
}
