using System;
using System.Collections.Generic;

public class Sample
{
    public void Print(IEnumerable<int> values)
    {
        /*^*/foreach (var value in values)
        {
            Console.WriteLine(value);
        }
    }
}
