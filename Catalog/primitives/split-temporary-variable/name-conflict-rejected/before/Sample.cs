using System;

public class Sample
{
    public void Print(int a, int b)
    {
        int value = a;
        Console.WriteLine(value);
        /*^*/value = b;
        Console.WriteLine(value);
    }
}
