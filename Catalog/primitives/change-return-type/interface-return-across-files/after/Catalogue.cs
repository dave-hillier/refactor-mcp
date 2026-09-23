using System.Collections.Generic;

namespace Shop;

public class Catalogue
{
    public IEnumerable<string> Names()
    {
        return new List<string> { "pen", "ink" };
    }
}
