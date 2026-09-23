using System.Collections.Generic;

namespace Shop
{
    public class Store
    {
        private readonly List<string> _items = new List<string>();

        public bool Add(string item)
        {
            if (string.IsNullOrEmpty(item))
            {
                return false;
            }

            if (_items.Contains(item))
            {
                return false;
            }

            _items.Add(item);
            return true;
        }

        public string Describe(string item)
        {
            if (!Add(item))
            {
                return "Rejected " + item;
            }
            else
            {
                return "Added " + item;
            }
        }

        public int Count(string item)
        {
            if (Add(item))
                return 1;
            return 0;
        }
    }
}
