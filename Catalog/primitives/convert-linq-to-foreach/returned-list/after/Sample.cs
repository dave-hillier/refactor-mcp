using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<int> Doubled(IEnumerable<int> values)
    {
        var result = new List<int>();
        foreach (var value in values)
        {
            result.Add(value * 2);
        }

        return result;
    }
}
