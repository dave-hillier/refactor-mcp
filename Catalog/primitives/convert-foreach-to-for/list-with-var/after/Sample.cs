using System;
using System.Collections.Generic;

public class Sample
{
    public void Print(List<string> names)
    {
        for (int i = 0; i < names.Count; i++)
        {
            var name = names[i];
            Console.WriteLine(name);
        }
    }
}
