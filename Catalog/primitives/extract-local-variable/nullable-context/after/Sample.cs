public class Sample
{
    public string Describe(int id)
    {
        string? trimmed = Find(id)?.Trim();
        return trimmed ?? "none";
    }

    private string? Find(int id) => id > 0 ? " found " : null;
}
