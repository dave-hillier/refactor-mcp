namespace Shop
{
    public class Container<T>
    {
        public Container(T item) => Item = item;

        public T Item { get; }

        public Container<T> Copy() => new Container<T>(Item);
    }
}
