using System;

public class Sample
{
    public void Run(string name)
    {
        Console.WriteLine("Start");

        // The greeting to show.
        string greeting;
        greeting = "Hello " + name; // built once
        Console.WriteLine(greeting);
    }
}
