using System.Collections.Generic;

namespace Shop
{
    public class Repository<T>
    {
        private readonly List<T> _items = new List<T>();
        private const string Separator = ", ";

        public string Describe()
        {
            return string.Join(Separator, _items);
        }
    }
}
