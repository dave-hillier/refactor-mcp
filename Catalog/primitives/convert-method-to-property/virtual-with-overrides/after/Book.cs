namespace Shop
{
    public class Book : Product
    {
        public override string GetKind() => "book";

        public override string Label => "book " + base.Label;
    }
}
