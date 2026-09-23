using System.Collections.Generic;

public class Sample
{
    public List<string> LongWords(string[] words)
    {
        // Collect the long words.
        var longWords = new List<string>();

        // Only words over five letters.
        /*^*/foreach (var word in words)
        {
            if (word.Length > 5)
            {
                longWords.Add(word);
            }
        }

        return longWords;
    }
}
