using System;
using System.Collections.Generic;

public class Sample
{
    public void Print(List<(string Name, int Age)> people)
    {
        for (int i = 0; i < people.Count; i++)
        {
            var (name, age) = people[i];
            Console.WriteLine(name + " " + age);
        }
    }
}
