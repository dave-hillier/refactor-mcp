using System.Linq;

public class Sample
{
    public int Total(int[] values)
    {
        int total = 0;
        foreach (var value in values)
        {
            total += value;
        }
        return total;
    }
}
