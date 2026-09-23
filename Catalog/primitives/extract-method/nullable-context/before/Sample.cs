using System;

public class Sample
{
    public string Greet(string first, string? middle)
    {
        var greeting = "Hello " + first;
        /*[*/Console.WriteLine(greeting.Length);
        Console.WriteLine(middle ?? "no middle name");/*]*/
        return greeting;
    }
}
