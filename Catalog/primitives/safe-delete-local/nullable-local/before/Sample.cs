using System.Collections.Generic;

public class Sample
{
    private readonly Dictionary<string, string> _names = new Dictionary<string, string>();

    public int Count(string key)
    {
        string? /*^*/name = Find(key);
        return _names.Count;
    }

    private string? Find(string key) => _names.TryGetValue(key, out var value) ? value : null;
}
