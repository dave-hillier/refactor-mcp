using System;

public class Sample
{
    public void Print(int[] values)
    {
        for (int index = 0; index < values.Length; index++)
        {
            var value = values[index];
            Console.WriteLine(value);
        }
    }
}
