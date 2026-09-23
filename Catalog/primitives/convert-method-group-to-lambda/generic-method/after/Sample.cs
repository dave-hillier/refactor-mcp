using System;
using System.Collections.Generic;

public class Sample
{
    public List<object> Run()
    {
        Func<string, List<object>> wrap = item => Wrap<object>(item);
        return wrap("a");
    }

    private static List<T> Wrap<T>(T item) => new List<T> { item };
}
