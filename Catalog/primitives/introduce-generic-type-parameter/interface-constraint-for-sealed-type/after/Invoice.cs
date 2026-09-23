namespace Shop
{
    public interface IPriced
    {
        decimal Total { get; }
    }

    public sealed class Invoice : IPriced
    {
        public decimal Total { get; set; }
    }
}
