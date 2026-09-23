namespace Shop
{
    public class Order : IOrder
    {
        public decimal Total { get; private set; }

        public void Add(decimal amount)
        {
            Total += amount;
            Log(amount);
        }

        public void Clear() => Total = 0;

        private void Log(decimal amount)
        {
        }
    }
}
