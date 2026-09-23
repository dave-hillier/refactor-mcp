namespace Shop
{
    public abstract class Product
    {
        public abstract string GetKind();

        public virtual string GetLabel() => "product";

        public string Describe() => GetKind() + ": " + GetLabel();
    }
}
