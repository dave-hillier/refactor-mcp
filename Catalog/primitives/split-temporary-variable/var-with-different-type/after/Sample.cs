using System;

public class Sample
{
    public void Show(object first)
    {
        var shown = first;
        Console.WriteLine(shown);
        object fallback = "none";
        Console.WriteLine(fallback);
    }
}
