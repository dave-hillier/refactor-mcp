using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<string> Trimmed(string[] words)
    {
        var trimmed = /*^*/words.Select(word => word.Trim()).Where(word => word.Length > 0).ToList();
        return trimmed;
    }
}
