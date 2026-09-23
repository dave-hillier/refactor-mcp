using System.Collections.Generic;

namespace Shop
{
    public class Basket<T>
    {
        private readonly List<T> _items = new List<T>();

        public IReadOnlyList<T> Items => _items.AsReadOnly();

        public void AddItem(T item) => _items.Add(item);

        public bool RemoveItem(T item) => _items.Remove(item);
    }

    public class Till
    {
        public int Scan(Basket<int> basket)
        {
            basket.AddItem(42);
            return basket.Items[0];
        }
    }
}
