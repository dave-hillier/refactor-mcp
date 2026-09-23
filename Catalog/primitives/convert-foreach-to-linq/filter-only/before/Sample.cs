using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<int> Positives(int[] numbers)
    {
        List<int> positives = new List<int>();
        /*^*/foreach (int n in numbers)
            if (n > 0)
                positives.Add(n);
        return positives;
    }
}
