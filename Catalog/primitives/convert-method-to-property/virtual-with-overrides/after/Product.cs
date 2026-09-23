namespace Shop
{
    public abstract class Product
    {
        public abstract string GetKind();

        public virtual string Label => "product";

        public string Describe() => GetKind() + ": " + Label;
    }
}
