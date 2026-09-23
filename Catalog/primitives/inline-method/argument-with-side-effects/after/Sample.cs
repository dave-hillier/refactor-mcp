using System;

public class Sample
{
    private int _counter;

    public void Run()
    {
        int value = Next();
        Console.WriteLine(value);
        Console.WriteLine(value * 2);
    }

    private int Next() => ++_counter;
}
