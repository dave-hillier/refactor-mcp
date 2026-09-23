using System;

public class Sample
{
    public void Run(string user)
    {
        Greet(user);
        Console.WriteLine("Done");
    }

    private void Greet(string name)
    {
        Console.WriteLine("Hello");
        Console.WriteLine(name);
    }
}
