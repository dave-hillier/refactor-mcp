namespace Shop
{
    public class Box
    {
    }

    public class Box<T>
    {
        public T Item { get; set; }

        public Box<T> Copy() => new Box<T> { Item = Item };
    }

    public class Shelf
    {
        public Box First { get; } = new Box();
    }
}
