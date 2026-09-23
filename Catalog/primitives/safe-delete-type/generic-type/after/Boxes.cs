namespace Shop
{
    public class Box
    {
    }

    public class Shelf
    {
        public Box First { get; } = new Box();
    }
}
