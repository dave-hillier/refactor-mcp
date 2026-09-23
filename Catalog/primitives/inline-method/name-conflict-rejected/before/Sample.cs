using System;

public class Sample
{
    public void Run(int a)
    {
        var total = a + 1;
        Print(total);
    }

    private void Print(int value)
    {
        var total = value * 2;
        Console.WriteLine(total);
    }
}
