using System;

namespace Shop
{
    public static class Text
    {
        public static string Shout(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            return text.ToUpperInvariant() + "!";
        }

        public static void Log(string text) => Console.WriteLine(text);
    }
}
