using System;
using System.Collections.Generic;

public class Sample
{
    public void PrintAll<T>(IReadOnlyList<T> entries)
    {
        foreach (T entry in entries)
        {
            Console.WriteLine(entry);
        }
    }
}
