public class Sample
{
    public int SumTo(int limit, int step)
    {
        return Sum(0, limit, step);
    }

    private static int Sum(int from, int limit, int step) => from > limit ? 0 : from + Sum(from + step, limit, step);
}
