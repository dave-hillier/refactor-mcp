using System;

public class Sample
{
    public string Greet(string first, string? middle)
    {
        var greeting = "Hello " + first;
        Log(greeting, middle);
        return greeting;
    }

    private void Log(string greeting, string? middle)
    {
        Console.WriteLine(greeting.Length);
        Console.WriteLine(middle ?? "no middle name");
    }
}
