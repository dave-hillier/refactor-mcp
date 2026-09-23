using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<int> Positives(int[] numbers)
    {
        List<int> positives = numbers.Where(n => n > 0)./*^*/ToList();
        return positives;
    }
}
