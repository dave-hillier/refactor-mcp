using System;

public class Sample
{
    public void Print(int a)
    {
        int value = a;
        /*^*/value += 1;
        Console.WriteLine(value);
    }
}
