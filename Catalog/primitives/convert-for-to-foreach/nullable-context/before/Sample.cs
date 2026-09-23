using System.Collections.Generic;

public class Sample
{
    public int CountMissing(List<string?> labels)
    {
        var missing = 0;
        /*^*/for (int i = 0; i < labels.Count; i++)
        {
            if (labels[i] is null)
            {
                missing++;
            }
        }

        return missing;
    }
}
