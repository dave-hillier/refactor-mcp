namespace Support
{
    public class Ticket
    {
        public string Describe(int code)
        {
            var label = "unknown";
            /*^*/if (code == 1)
            {
                label = "new";
            }
            else if (code == 2)
            {
                label = "open";
            }

            return label;
        }
    }
}
