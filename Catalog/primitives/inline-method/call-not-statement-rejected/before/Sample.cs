using System;

public class Sample
{
    public Action Later() => () => Greet();

    private void Greet()
    {
        Console.WriteLine("Hello");
        Console.WriteLine("World");
    }
}
