public class Sample
{
    public int Sum(int start, int[] values)
    {
        var total = start;
        /*^*/foreach (var value in values)
        {
            total += value;
        }

        return total;
    }
}
