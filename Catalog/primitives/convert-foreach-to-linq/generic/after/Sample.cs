using System;
using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<T> Matching<T>(IEnumerable<T> items, Func<T, bool> predicate)
    {
        var result = items.Where(item => predicate(item)).ToList();

        return result;
    }
}
