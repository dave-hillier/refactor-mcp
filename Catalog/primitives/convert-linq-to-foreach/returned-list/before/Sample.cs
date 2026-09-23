using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<int> Doubled(IEnumerable<int> values)
    {
        return /*^*/values.Select(value => value * 2).ToList();
    }
}
