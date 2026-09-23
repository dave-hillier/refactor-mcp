using System;
using System.Linq;

public class Sample
{
    public void Report(int[] values)
    {
        Console.WriteLine(/*^*/values.Count(value => value > 0));
    }
}
