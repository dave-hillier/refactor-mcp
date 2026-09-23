public static class Sample
{
    public static string Label(string name)
    {
        return Clean(name) + ":";

        static string /*^*/Clean(string text) => text.Trim();
    }
}
