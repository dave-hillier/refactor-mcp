using System.Linq;

public class Sample
{
    public int LongWords(string[] words)
    {
        var count = words.Count(word => word.Length > 3);

        return count;
    }
}
