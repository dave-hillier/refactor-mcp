using System.Collections.Generic;

namespace Shop
{
    public class Repository<T>
    {
        private readonly List<T> _items = new List<T>();

        public string Describe()
        {
            return string.Join(/*[*/", "/*]*/, _items);
        }
    }
}
