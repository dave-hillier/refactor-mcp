using System;

public class Sample
{
    public void Greet(string name)
    {
        var text = "Hello " + name;
        Console.WriteLine(text);

        // Now say goodbye.
        var farewell = "Goodbye " + name; // shown last
        Console.WriteLine(farewell);
    }
}
