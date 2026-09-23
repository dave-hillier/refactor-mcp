using System;
using System.Collections.Generic;

public class Sample
{
    public List<T> Twice<T>(T item) where T : IComparable<T>
    {
        return Pair();

        List<T> /*^*/Pair() => new List<T> { item, item };
    }
}
