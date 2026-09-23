namespace Shop;

public static class TextExtensions
{
    public static string Truncate(this string text, int length)
    {
        return text.Length <= length ? text : text.Substring(0, length);
    }
}
