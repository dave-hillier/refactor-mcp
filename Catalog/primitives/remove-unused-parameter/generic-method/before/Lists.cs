using System.Collections.Generic;

namespace Shop;

public static class Lists
{
    public static T First<T>(List<T> items, bool cached)
    {
        return items[0];
    }

    public static int Use(List<int> numbers)
    {
        return First(numbers, true) + First<int>(numbers, false);
    }
}
