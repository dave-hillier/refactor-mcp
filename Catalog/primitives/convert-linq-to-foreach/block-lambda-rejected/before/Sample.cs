using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<int> Positives(int[] values)
    {
        var positives = /*^*/values.Where(value => { return value > 0; }).ToList();
        return positives;
    }
}
