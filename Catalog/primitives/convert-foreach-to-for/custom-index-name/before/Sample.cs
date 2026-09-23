using System;

public class Sample
{
    public void Print(int[] values)
    {
        /*^*/foreach (var value in values)
        {
            Console.WriteLine(value);
        }
    }
}
