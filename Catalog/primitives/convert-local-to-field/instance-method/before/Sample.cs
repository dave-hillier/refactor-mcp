using System;

public class Sample
{
    public void Add(int a, int b)
    {
        var /*^*/total = a + b;
        Console.WriteLine(total);
    }
}
