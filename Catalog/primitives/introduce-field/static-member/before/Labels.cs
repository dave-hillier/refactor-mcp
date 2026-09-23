namespace Shop
{
    public static class Labels
    {
        public static string Shout(string name)
        {
            return /*[*/name.ToUpperInvariant()/*]*/ + "!";
        }
    }
}
