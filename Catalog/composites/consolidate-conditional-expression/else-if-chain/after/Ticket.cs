namespace Support
{
    public class Ticket
    {
        public string Describe(int code)
        {
            string label;
            if (code == 1 || code == 2)
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
