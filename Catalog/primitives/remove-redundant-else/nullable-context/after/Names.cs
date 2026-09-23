namespace People
{
    public class Names
    {
        public string Initial(string? name)
        {
            if (name is null or "")
            {
                return "?";
            }

            return name.Substring(0, 1);
        }
    }
}
