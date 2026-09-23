using System;
using System.Collections.Generic;

public class Sample
{
    public List<int> Run()
    {
        Func<int, List<int>> wrap = Wrap;
        return wrap(4);
    }

    private static List<T> Wrap<T>(T item) => new List<T> { item };
}
