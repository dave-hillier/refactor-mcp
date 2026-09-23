public class Sample
{
    public int Sum(int[] values)
    {
        var total = 0;
        foreach (int value in values)
        {
            total += value;
        }

        return total;
    }
}
