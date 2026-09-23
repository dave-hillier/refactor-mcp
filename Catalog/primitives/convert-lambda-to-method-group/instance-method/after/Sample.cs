using System.Collections.Generic;
using System.Linq;

public class Sample
{
    private int _minimum = 3;

    public int CountValid(IEnumerable<int> values)
    {
        return values.Count(this.IsValid);
    }

    private bool IsValid(int value) => value >= _minimum;
}
