using System;

public static class Sample
{
    public static int Twice(int value)
    {
        var doubled = value * 2;
        Report(doubled);
        return doubled;
    }

    private static void Report(int doubled)
    {
        Console.WriteLine(doubled);
    }
}
