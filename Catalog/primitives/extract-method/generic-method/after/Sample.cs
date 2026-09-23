using System;
using System.Collections.Generic;

public class Sample
{
    public T Largest<T>(List<T> items) where T : IComparable<T>
    {
        var largest = items[0];
        foreach (var item in items)
        {
            if (item.CompareTo(largest) > 0)
                largest = item;
        }

        Report(largest, items);
        return largest;
    }

    private void Report<T>(T largest, List<T> items) where T : IComparable<T>
    {
        Console.WriteLine($"{largest} of {items.Count}");
    }
}
