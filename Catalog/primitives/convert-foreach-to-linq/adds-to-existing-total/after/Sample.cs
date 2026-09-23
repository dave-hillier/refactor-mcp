using System.Linq;

public class Sample
{
    public int Sum(int start, int[] values)
    {
        var total = start;
        total += values.Sum();

        return total;
    }
}
