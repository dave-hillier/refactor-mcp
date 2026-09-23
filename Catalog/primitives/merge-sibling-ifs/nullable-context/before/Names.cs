namespace People
{
    public class Names
    {
        public string Initial(string? name)
        {
            /*^*/if (name is null)
            {
                return "?";
            }

            if (name.Length == 0)
            {
                return "?";
            }

            return name.Substring(0, 1);
        }
    }
}
