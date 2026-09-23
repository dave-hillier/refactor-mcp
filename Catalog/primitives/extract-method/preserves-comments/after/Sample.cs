using System;

public class Sample
{
    public void Run(string name)
    {
        // Greet first.
        Log(name);

        Console.WriteLine("Done");
    }

    private void Log(string name)
    {
        Console.WriteLine("Hello");
        // Then name the caller.
        Console.WriteLine(name); // trailing note
    }
}
