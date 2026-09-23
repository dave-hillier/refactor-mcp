namespace Shop
{
    public static class Text
    {
        public static string Shout(this string text) => text.ToUpperInvariant();

        public static string Maybe(string text) => text?.Shout();
    }
}
