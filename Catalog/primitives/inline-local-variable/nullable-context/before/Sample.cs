public class Sample
{
    public string Describe(int id)
    {
        string? /*^*/name = Find(id);
        return name ?? "unknown";
    }

    private string? Find(int id) => id > 0 ? "found" : null;
}
