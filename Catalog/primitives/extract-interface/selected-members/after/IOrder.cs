namespace Shop
{
    public interface IOrder
    {
        decimal Total { get; }
        void Add(decimal amount);
    }
}
