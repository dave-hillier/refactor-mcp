using System;

public class Sample
{
    private int _count;

    public int Run()
    {
        Func<int> next = /*^*/Next;
        return next();
    }

    private int Next() => ++_count;
}
