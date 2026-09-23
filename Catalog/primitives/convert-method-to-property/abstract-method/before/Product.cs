namespace Shop
{
    public abstract class Product
    {
        public abstract string GetKind();
    }

    public class Book : Product
    {
        public override string GetKind() => "book";
    }
}
