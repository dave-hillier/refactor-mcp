using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public int CountMissing(List<string?> labels)
    {
        var missing = /*^*/labels.Count(label => label is null);

        return missing;
    }
}
