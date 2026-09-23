using System;

public class Sample
{
    public void Run(int a, int b)
    {
        int value = a;
        Console.WriteLine(value);
        /*^*/value = b;
        Console.WriteLine(value);
        if (a > b)
        {
            value = a - b;
        }

        Console.WriteLine(value);
    }
}
