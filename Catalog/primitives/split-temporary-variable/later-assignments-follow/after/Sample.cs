using System;

public class Sample
{
    public void Run(int a, int b)
    {
        int value = a;
        Console.WriteLine(value);
        int second = b;
        Console.WriteLine(second);
        if (a > b)
        {
            second = a - b;
        }

        Console.WriteLine(second);
    }
}
