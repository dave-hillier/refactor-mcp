public class Sample
{
    public int Sum(int[] values)
    {
        var total = 0;
        var i = 0;
        /*^*/while (i < values.Length)
        {
            total += values[i++];
        }

        return total;
    }
}
