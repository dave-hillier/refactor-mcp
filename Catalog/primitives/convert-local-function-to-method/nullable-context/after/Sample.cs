public class Sample
{
    public string Label(string? name, string? fallback)
    {
        return Clean(name, fallback) ?? "none";
    }

    private static string? Clean(string? text, string? fallback) => text?.Trim() ?? fallback;
}
