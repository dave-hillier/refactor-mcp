using System;

public class Sample
{
    public int Sum(int[] values)
    {
        var total = 0;
        /*^*/foreach (var value in values)
        {
            Console.WriteLine(value);
            total += value;
        }

        return total;
    }
}
