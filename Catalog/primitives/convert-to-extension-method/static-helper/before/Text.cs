namespace Shop
{
    public static class Text
    {
        /// <summary>Upper-cases and exclaims.</summary>
        public static string Shout(string text) => text.ToUpperInvariant() + "!";

        public static string Greet(string name) => Shout("hello " + name);
    }
}
