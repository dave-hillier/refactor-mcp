using System.Collections.Generic;

public class Sample
{
    public int Count()
    {
        Dictionary<string, int> /*^*/counts = new();
        return Fill(counts);
    }

    private int Fill(Dictionary<string, int> counts)
    {
        counts["a"] = 1;
        return counts.Count;
    }
}
