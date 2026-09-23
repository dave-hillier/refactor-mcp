namespace Shop;

public class Notes
{
    public string Add(string text, string? author = null)
    {
        return text + (author ?? "");
    }

    public string Both()
    {
        return Add("first") + Add("second", "sam");
    }
}
