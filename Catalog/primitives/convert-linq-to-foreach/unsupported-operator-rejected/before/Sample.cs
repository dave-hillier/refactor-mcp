using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<int> Sorted(int[] values)
    {
        var sorted = /*^*/values.OrderBy(value => value).ToList();
        return sorted;
    }
}
