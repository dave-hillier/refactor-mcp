namespace Support
{
    public class Ticket
    {
        public string Describe(int code)
        {
            string label;
            /*^*/if (code == 1)
            {
                label = "new";
            }
            else if (code == 2 || code == 3)
            {
                label = "open";
            }
            else
            {
                label = "closed";
            }

            return label;
        }
    }
}
