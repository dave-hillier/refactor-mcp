using System;

public class Sample
{
    public int Run()
    {
        Func<int, int, int> combine = (a, b) /*^*/=> Add(a, b);
        return combine(2, 3);
    }

    private static int Add(int left, int right) => left + right;
}
