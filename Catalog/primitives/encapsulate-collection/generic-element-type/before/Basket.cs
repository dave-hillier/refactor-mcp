using System.Collections.Generic;

namespace Shop
{
    public class Basket<T>
    {
        private readonly List<T> _items = new List<T>();

        public List<T> Items => _items;
    }

    public class Till
    {
        public int Scan(Basket<int> basket)
        {
            basket.Items.Add(42);
            return basket.Items[0];
        }
    }
}
