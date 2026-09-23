using System.Linq;

public class Sample
{
    public int Total(int[] values)
    {
        var total = /*^*/values.Sum();
        return total;
    }
}
