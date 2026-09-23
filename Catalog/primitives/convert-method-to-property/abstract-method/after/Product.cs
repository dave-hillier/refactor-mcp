namespace Shop
{
    public abstract class Product
    {
        public abstract string Kind { get; }
    }

    public class Book : Product
    {
        public override string Kind => "book";
    }
}
