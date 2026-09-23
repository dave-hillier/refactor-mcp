using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public int CountMissing(List<string?> labels)
    {
        int missing = 0;
        foreach (var label in labels)
        {
            if (label is null)
            {
                missing++;
            }
        }

        return missing;
    }
}
