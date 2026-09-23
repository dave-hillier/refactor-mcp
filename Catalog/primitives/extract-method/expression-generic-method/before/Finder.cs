using System.Collections.Generic;

namespace Search
{
    public class Finder
    {
        public int IndexOf<T>(List<T> items, T wanted) where T : class
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (/*[*/EqualityComparer<T>.Default.Equals(items[i], wanted)/*]*/)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
