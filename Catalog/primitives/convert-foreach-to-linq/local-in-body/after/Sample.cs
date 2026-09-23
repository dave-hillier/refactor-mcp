using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<int> Lengths(IEnumerable<string> words)
    {
        var lengths = words.Select(word => word.Trim()).Where(trimmed => trimmed.Length > 0).Select(trimmed => trimmed.Length).ToList();

        return lengths;
    }
}
