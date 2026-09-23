using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<string> Clean(List<string> names)
    {
        return names.Select(/*^*/name => name.Trim()).ToList();
    }
}
