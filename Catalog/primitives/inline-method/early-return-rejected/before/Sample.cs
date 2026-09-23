using System;

public class Sample
{
    public void Run(string text)
    {
        Log(text);
        Console.WriteLine("Done");
    }

    private void Log(string text)
    {
        if (text.Length == 0)
            return;
        Console.WriteLine(text);
    }
}
