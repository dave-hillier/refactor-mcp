namespace Shop
{
    public class Book : Product
    {
        public override string GetKind() => "book";

        public override string GetLabel() => "book " + base.GetLabel();
    }
}
