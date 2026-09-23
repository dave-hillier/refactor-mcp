public class Sample
{
    public string Label(string? name)
    {
        return Clean(name) ?? "none";

        static string? Clean(string? name) => name?.Trim();
    }
}
