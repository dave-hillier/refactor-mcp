using System.Linq;

public class Sample
{
    public int LongWords(string[] words)
    {
        int count = words./*^*/Count(word => word.Length > 3);

        return count;
    }
}
