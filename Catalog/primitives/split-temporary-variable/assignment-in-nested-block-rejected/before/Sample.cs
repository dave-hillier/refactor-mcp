using System;

public class Sample
{
    public void Print(int a, bool reset)
    {
        int value = a;
        if (reset)
        {
            /*^*/value = 0;
        }

        Console.WriteLine(value);
    }
}
