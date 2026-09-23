namespace Shop
{
    public class Product
    {
        public virtual string Label => "product";
    }

    public class Book : Product
    {
        public override string Label => "book";
    }
}
