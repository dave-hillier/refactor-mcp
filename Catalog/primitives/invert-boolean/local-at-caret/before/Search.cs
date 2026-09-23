using System.Collections.Generic;

namespace Shop
{
    public class Search
    {
        public bool Contains(List<int> items, int target)
        {
            bool /*^*/found = false;
            foreach (var item in items)
            {
                if (item == target)
                {
                    found = true;
                }
            }

            return found;
        }
    }
}
