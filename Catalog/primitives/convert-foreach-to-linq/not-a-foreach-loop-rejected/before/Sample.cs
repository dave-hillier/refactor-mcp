public class Sample
{
    public int Sum(int[] values)
    {
        var total = 0;
        /*^*/for (int i = 0; i < values.Length; i++)
        {
            total += values[i];
        }

        return total;
    }
}
