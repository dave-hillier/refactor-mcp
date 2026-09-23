using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<string> LongWords(string[] words)
    {
        // Collect the long words.
        // Only words over five letters.
        var longWords = words.Where(word => word.Length > 5).ToList();

        return longWords;
    }
}
