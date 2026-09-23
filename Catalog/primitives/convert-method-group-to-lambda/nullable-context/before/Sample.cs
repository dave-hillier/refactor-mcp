using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<string?> Normalise(List<string?> names)
    {
        return names.Select(/*^*/Trim).ToList();
    }

    private static string? Trim(string? name) => name?.Trim();
}
