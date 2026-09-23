using System.Collections.Generic;
using System.Linq;

public class Formatter
{
    public string Format(string text) => text.ToUpperInvariant();
}

public class Sample
{
    public List<string> FormatAll(List<string> names)
    {
        return names.Select(/*^*/name => CreateFormatter().Format(name)).ToList();
    }

    private static Formatter CreateFormatter() => new Formatter();
}
