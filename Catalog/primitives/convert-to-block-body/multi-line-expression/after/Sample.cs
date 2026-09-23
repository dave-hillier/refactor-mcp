using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public int Total(List<int> items)
    {
        return items
            .Where(i => i > 0)
            .Sum();
    }
}
