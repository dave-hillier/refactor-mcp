namespace Shop;

public static class TextExtensions
{
    public static string Truncate(this string text, int length, string ellipsis)
    {
        return text.Length <= length ? text : text.Substring(0, length);
    }
}

public class Labels
{
    public string Short(string name)
    {
        return name.Truncate(10, "...") + TextExtensions.Truncate(name, 3, "...");
    }
}
