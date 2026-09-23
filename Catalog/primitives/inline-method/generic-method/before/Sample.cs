using System.Collections.Generic;

public class Sample
{
    public List<string> Names() => Pair("a", "b");

    private static List<T> Pair<T>(T first, T second) => new List<T> { first, second };
}
