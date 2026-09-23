using System.Collections.Generic;

namespace Shop
{
    public class Search
    {
        public bool Contains(List<int> items, int target)
        {
            bool missing = true;
            foreach (var item in items)
            {
                if (item == target)
                {
                    missing = false;
                }
            }

            return !missing;
        }
    }
}
