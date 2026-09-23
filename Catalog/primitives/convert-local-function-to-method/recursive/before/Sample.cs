public class Sample
{
    public int SumTo(int limit, int step)
    {
        return /*^*/Sum(0);

        int Sum(int from) => from > limit ? 0 : from + Sum(from + step);
    }
}
