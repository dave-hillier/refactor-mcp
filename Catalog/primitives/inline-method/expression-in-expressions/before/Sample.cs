public class Sample
{
    public int Calc(int a, int b)
    {
        return Doubled(a) + Doubled(b + 1);
    }

    private int Doubled(int x)
    {
        return x * 2;
    }
}
