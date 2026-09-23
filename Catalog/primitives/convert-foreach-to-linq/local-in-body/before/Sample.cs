using System.Collections.Generic;

public class Sample
{
    public List<int> Lengths(IEnumerable<string> words)
    {
        var lengths = new List<int>();
        /*^*/foreach (var word in words)
        {
            var trimmed = word.Trim();
            if (trimmed.Length > 0)
            {
                lengths.Add(trimmed.Length);
            }
        }

        return lengths;
    }
}
