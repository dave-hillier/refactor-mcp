using System;

public class Sample
{
    public int Calc(int a, int b)
    {
        int limit = 10;
        var explainPositiveResult = ExplainPositive(a, limit, b);
        if (explainPositiveResult != null)
        {
            return explainPositiveResult.Value;
        }

        return 0;
    }

    private int? ExplainPositive(int a, int limit, int b)
    {
        if (a > limit)
        {
            return a + b;
        }

        return default;
    }
}
