using System.Collections.Generic;
using System.Linq;

public class Sample
{
    private string _prefix = "#";

    public List<string> Report(int first, int[] rest)
    {
        var lines = new List<string> { Describe(first) };
        lines.AddRange(rest.Select(n => Describe(n)));
        return lines;

        string Describe(int number)
        {
            return _prefix + number;
        }
    }
}
