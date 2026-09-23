namespace Shop;

partial class Cache<T> where T : class
{
    public void Store(string key, T value) => _items[key] = value;

    public T Find(string key) => _items.TryGetValue(key, out var value) ? value : null;
}
