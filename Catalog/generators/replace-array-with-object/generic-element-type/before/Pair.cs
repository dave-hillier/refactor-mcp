namespace Shop
{
    public class Pair<T>
    {
        private readonly T[] _items = new T[2];

        public T Left => _items[0];

        public T Right => _items[1];
    }
}
