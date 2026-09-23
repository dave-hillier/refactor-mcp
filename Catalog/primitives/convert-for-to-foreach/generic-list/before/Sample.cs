using System;
using System.Collections.Generic;

public class Sample
{
    public void PrintAll<T>(IReadOnlyList<T> entries)
    {
        /*^*/for (int i = 0; i < entries.Count; i++)
        {
            Console.WriteLine(entries[i]);
        }
    }
}
