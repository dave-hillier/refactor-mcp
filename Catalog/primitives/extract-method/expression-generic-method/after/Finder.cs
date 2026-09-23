using System.Collections.Generic;

namespace Search
{
    public class Finder
    {
        public int IndexOf<T>(List<T> items, T wanted) where T : class
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (Matches(items, i, wanted))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool Matches<T>(List<T> items, int i, T wanted) where T : class
        {
            return EqualityComparer<T>.Default.Equals(items[i], wanted);
        }
    }
}
