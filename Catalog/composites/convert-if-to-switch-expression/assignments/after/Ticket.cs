namespace Support
{
    public class Ticket
    {
        public string Describe(int code)
        {
            string label;
            label = code switch
            {
                1 => "new",
                2 or 3 => "open",
                _ => "closed",
            };

            return label;
        }
    }
}
