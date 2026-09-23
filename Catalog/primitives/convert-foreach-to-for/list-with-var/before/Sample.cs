using System;
using System.Collections.Generic;

public class Sample
{
    public void Print(List<string> names)
    {
        foreach (var name /*^*/in names)
        {
            Console.WriteLine(name);
        }
    }
}
