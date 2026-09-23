using System.Collections.Generic;

public class Sample
{
    public int CountMissing(List<string?> labels)
    {
        var missing = 0;
        /*^*/foreach (var label in labels)
        {
            if (label is null)
            {
                missing++;
            }
        }

        return missing;
    }
}
