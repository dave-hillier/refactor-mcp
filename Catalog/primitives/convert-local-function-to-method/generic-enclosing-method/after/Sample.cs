using System;
using System.Collections.Generic;

public class Sample
{
    public List<T> Twice<T>(T item) where T : IComparable<T>
    {
        return Pair(item);
    }

    private static List<T> Pair<T>(T item) where T : IComparable<T> => new List<T> { item, item };
}
