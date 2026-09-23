using System;

public class Sample
{
    public int Calc(int a)
    {
        Func<int, int> twice = Doubled;
        return twice(a) + Doubled(a);
    }

    private int Doubled(int x) => x * 2;
}
