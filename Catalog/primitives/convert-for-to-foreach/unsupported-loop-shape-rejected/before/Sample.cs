public class Sample
{
    public int SumTail(int[] values)
    {
        var total = 0;
        /*^*/for (int i = 1; i < values.Length; i++)
        {
            total += values[i];
        }

        return total;
    }
}
