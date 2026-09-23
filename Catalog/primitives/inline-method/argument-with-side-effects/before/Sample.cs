using System;

public class Sample
{
    private int _counter;

    public void Run()
    {
        Show(Next());
    }

    private void Show(int value)
    {
        Console.WriteLine(value);
        Console.WriteLine(value * 2);
    }

    private int Next() => ++_counter;
}
