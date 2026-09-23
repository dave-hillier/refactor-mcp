using System;

public class Sample
{
    public void Print(int i, int[] values)
    {
        /*^*/foreach (var value in values)
        {
            Console.WriteLine(i * value);
        }
    }
}
