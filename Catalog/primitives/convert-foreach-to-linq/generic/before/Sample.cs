using System;
using System.Collections.Generic;

public class Sample
{
    public List<T> Matching<T>(IEnumerable<T> items, Func<T, bool> predicate)
    {
        var result = new List<T>();
        /*^*/foreach (var item in items)
        {
            if (predicate(item))
            {
                result.Add(item);
            }
        }

        return result;
    }
}
