using System;

public class Sample
{
    public void Print(int a)
    {
        int value = a;
        Console.WriteLine(value);
        /*^*/value = value * 2;
        Console.WriteLine(value);
    }
}
