using System.Collections.Generic;

public class Sample
{
    private readonly Dictionary<string, string> _names = new();

    public string? Find(string key)
    {
        return _names.TryGetValue(key, out var name) ? name : null;
    }
}
