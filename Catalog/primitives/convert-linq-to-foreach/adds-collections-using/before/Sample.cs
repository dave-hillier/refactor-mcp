using System.Linq;

public class Sample
{
    public int CountPositives(int[] numbers)
    {
        var positives = /*^*/numbers.Where(n => n > 0).ToList();
        return positives.Count;
    }
}
