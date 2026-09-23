using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public int CountPositives(int[] numbers)
    {
        var positives = new List<int>();
        foreach (var n in numbers)
        {
            if (n > 0)
            {
                positives.Add(n);
            }
        }
        return positives.Count;
    }
}
