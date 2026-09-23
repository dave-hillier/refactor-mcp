using System;
using System.Collections.Generic;

public class Sample
{
    public void Print(List<(string Name, int Age)> people)
    {
        /*^*/foreach (var (name, age) in people)
        {
            Console.WriteLine(name + " " + age);
        }
    }
}
