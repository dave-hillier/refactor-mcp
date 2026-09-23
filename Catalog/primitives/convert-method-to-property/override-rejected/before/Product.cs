namespace Shop
{
    public class Product
    {
        public virtual string GetLabel() => "product";
    }

    public class Book : Product
    {
        public override string GetLabel() => "book";
    }
}
