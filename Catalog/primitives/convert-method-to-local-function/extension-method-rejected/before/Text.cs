public static class Text
{
    public static string Greet(string name)
    {
        return name.Shout();
    }

    private static string Shout(this string text) => text.ToUpperInvariant() + "!";
}
