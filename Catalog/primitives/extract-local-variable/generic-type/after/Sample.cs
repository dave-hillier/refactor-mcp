using System.Collections.Generic;

public class Sample
{
    public int Count<T>(T item)
    {
        List<T> items = new List<T> { item, item };
        return Measure(items);
    }

    private int Measure<T>(List<T> items) => items.Count;
}
