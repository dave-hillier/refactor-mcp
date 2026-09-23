public class Sample
{
    public string Describe(int id)
    {
        return Find(id) ?? "unknown";
    }

    private string? Find(int id) => id > 0 ? "found" : null;
}
