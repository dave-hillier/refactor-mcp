namespace Shop
{
    public static class Text
    {
        // Used for headings.
        public static string Shout(string text) => text.ToUpperInvariant() + "!";

        public static string Greet(string name) => Shout("hello " + name);
    }
}
