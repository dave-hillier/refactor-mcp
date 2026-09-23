namespace Shop
{
    public class Product
    {
        public virtual string Name { get; set; } = "";
    }

    public class Book : Product
    {
        public override string Name { get; set; } = "";
    }
}
