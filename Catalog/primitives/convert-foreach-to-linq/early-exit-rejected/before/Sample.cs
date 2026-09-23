public class Sample
{
    public int SumUntilNegative(int[] values)
    {
        var total = 0;
        /*^*/foreach (var value in values)
        {
            if (value < 0)
                break;
            total += value;
        }

        return total;
    }
}
