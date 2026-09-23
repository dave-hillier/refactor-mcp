using System;

public class Sample
{
    public int Run()
    {
        Func<int, int, int> combine = (left, right) => Add(left, right);
        return combine(2, 3);
    }

    private static int Add(int left, int right) => left + right;
}
