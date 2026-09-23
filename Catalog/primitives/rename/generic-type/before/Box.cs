namespace Shop
{
    public class Box<T>
    {
        public Box(T item) => Item = item;

        public T Item { get; }

        public Box<T> Copy() => new Box<T>(Item);
    }
}
