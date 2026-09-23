using System;

public class Sample
{
    public string Run()
    {
        Func<int, string> describe = /*^*/n => Describe(n);
        return describe(3);
    }

    private static string Describe(long value) => value.ToString();
}
