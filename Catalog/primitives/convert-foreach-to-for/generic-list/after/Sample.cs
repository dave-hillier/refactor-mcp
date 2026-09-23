using System;
using System.Collections.Generic;

public class Sample
{
    public void PrintAll<T>(IReadOnlyList<T> entries)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            T entry = entries[i];
            Console.WriteLine(entry);
        }
    }
}
