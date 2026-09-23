namespace Shop;

public class Box<T>
{
    private Box(T value)
    {
        Value = value;
    }

    public static Box<T> Create(T value) => new Box<T>(value);

    public T Value { get; }
}

public static class Boxes
{
    public static Box<int> Five() => Box<int>.Create(5);

    public static Box<TItem> Of<TItem>(TItem item) => Box<TItem>.Create(item);
}
