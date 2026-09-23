public class Sample
{
    public int Grow(int[] values)
    {
        var total = 0;
        /*^*/foreach (var value in values)
        {
            if (value > total)
                total += value;
        }

        return total;
    }
}
