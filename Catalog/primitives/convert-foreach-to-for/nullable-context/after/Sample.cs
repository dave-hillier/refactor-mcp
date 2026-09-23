using System.Collections.Generic;

public class Sample
{
    public int CountMissing(IList<string?> labels)
    {
        var missing = 0;
        for (int i = 0; i < labels.Count; i++)
        {
            string? label = labels[i];
            if (label is null)
            {
                missing++;
            }
        }

        return missing;
    }
}
