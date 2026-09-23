namespace Shop
{
    public class Order
    {
        public decimal Total { get; set; }

        internal void Recalculate() => Total = 0;
    }
}
