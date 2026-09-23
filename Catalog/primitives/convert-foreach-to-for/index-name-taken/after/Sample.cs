using System;

public class Sample
{
    public void Print(int i, int[] values)
    {
        for (int j = 0; j < values.Length; j++)
        {
            var value = values[j];
            Console.WriteLine(i * value);
        }
    }
}
