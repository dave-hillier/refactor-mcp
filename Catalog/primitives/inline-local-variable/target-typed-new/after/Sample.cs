using System.Collections.Generic;

public class Sample
{
    public int Count()
    {
        return Fill(new Dictionary<string, int>());
    }

    private int Fill(Dictionary<string, int> counts)
    {
        counts["a"] = 1;
        return counts.Count;
    }
}
