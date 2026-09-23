using System.Linq;

public class Sample
{
    public int LongWords(string[] words)
    {
        int count = 0;
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
