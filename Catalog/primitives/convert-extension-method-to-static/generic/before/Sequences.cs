using System.Collections.Generic;

namespace Shop
{
    public static class Sequences
    {
        public static T FirstOr<T>(this IEnumerable<T> items, T fallback)
        {
            foreach (var item in items)
                return item;
            return fallback;
        }
    }
}
