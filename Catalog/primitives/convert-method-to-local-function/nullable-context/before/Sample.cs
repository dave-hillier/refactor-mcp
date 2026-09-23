public class Sample
{
    public string Label(string? name)
    {
        return Clean(name) ?? "none";
    }

    private static string? Clean(string? name) => name?.Trim();
}
