public class Sample
{
    public int LongWords(string[] words)
    {
        var count = 0;
        /*^*/foreach (var word in words)
        {
            if (word.Length > 3)
                count++;
        }

        return count;
    }
}
