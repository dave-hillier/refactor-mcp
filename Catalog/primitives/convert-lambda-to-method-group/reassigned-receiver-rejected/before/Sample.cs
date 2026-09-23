using System;

public class Formatter
{
    public string Format(string text) => text.ToUpperInvariant();
}

public class Sample
{
    public string Run(Formatter first, Formatter second)
    {
        var formatter = first;
        Func<string, string> format = /*^*/text => formatter.Format(text);
        formatter = second;
        return format("a");
    }
}
