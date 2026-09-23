using System.Collections.Generic;

public class Sample
{
    public int Count<T>(T item)
    {
        return Measure(/*[*/new List<T> { item, item }/*]*/);
    }

    private int Measure<T>(List<T> items) => items.Count;
}
