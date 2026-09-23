namespace People
{
    public class Names
    {
        public string Initial(string? name)
        {
            /*^*/if (name is null or "")
            {
                return "?";
            }
            else
            {
                return name.Substring(0, 1);
            }
        }
    }
}
