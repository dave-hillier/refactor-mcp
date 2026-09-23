using System;

public class Sample
{
    public void Print(int[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            var value = values[i];
            Console.WriteLine(value);
        }
    }
}
