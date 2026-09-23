using System.Collections.Generic;

public class Sample
{
    public T First<T>(List<T> items)
    {
        T /*^*/first = items[0];
        return first;
    }
}
