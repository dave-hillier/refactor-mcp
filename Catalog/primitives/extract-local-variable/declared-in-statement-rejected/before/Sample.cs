using System.Linq;

public class Sample
{
    public int[] Doubled(int[] values)
    {
        return values.Select(v => /*[*/v * 2/*]*/).ToArray();
    }
}
