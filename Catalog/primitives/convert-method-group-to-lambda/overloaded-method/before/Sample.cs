using System;
using System.Collections.Generic;

public class Sample
{
    public void Print(List<string> names)
    {
        names.ForEach(Console./*^*/WriteLine);
    }
}
