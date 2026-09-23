namespace Shop
{
    public static class Text
    {
        // Used for headings.
        public static string Shout(this string text) => text.ToUpperInvariant() + "!";

        public static string Greet(string name) => ("hello " + name).Shout();
    }
}
