using System.Linq;

public class Sample
{
    public int LongWords(string[] words)
    {
        // Words over three letters.
        int count = 0; // not trimmed
        foreach (var word in words)
        {
            if (word.Length > 3)
            {
                count++;
            }
        }

        return count;
    }
}
