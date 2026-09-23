public class Sample
{
    public string Describe(int id)
    {
        return /*[*/Find(id)?.Trim()/*]*/ ?? "none";
    }

    private string? Find(int id) => id > 0 ? " found " : null;
}
