using System;
using System.Linq;

public class Sample
{
    public void Report(int[] values, int target)
    {
        var found = values.Any(value => value == target);

        Console.WriteLine(found);
    }
}
