public static class Sample
{
    public static string Label(string name)
    {
        return Clean(name) + ":";
    }

    private static string Clean(string text) => text.Trim();
}
