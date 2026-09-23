using System.Collections.Generic;
using System.Linq;

public class Formatter
{
    public string Format(string text) => text.ToUpperInvariant();
}

public class Sample
{
    private readonly Formatter _formatter = new Formatter();

    public List<string> FormatAll(List<string> names)
    {
        return names.Select(/*^*/name => _formatter.Format(name)).ToList();
    }
}
