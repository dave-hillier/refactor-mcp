using System.Linq;

public class Sample
{
    public int LongWords(string[] words)
    {
        // Words over three letters.
        var count = /*^*/words.Count(word => word.Length > 3); // not trimmed

        return count;
    }
}
