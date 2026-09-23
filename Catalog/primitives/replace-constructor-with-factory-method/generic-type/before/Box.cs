namespace Shop;

public class Box<T>
{
    public Box(T value)
    {
        Value = value;
    }

    public T Value { get; }
}

public static class Boxes
{
    public static Box<int> Five() => new Box<int>(5);

    public static Box<TItem> Of<TItem>(TItem item) => new Box<TItem>(item);
}
