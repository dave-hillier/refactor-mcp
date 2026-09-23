using System;

public class Sample
{
    public void Log(string message)
    {
#if DEBUG
        Console.WriteLine("debug: " + message);
#else
        Console.WriteLine(message);
#endif
    }
}
