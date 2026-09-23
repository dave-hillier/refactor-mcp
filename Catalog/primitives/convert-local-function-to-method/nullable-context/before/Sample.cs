public class Sample
{
    public string Label(string? name, string? fallback)
    {
        return Clean(name) ?? "none";

        string? /*^*/Clean(string? text) => text?.Trim() ?? fallback;
    }
}
