namespace Shop
{
    public partial class Order
    {
        // Running total, in pence.
        private long _total;

        /// <summary>The order total.</summary>
        public long Total
        {
            get => _total;
            protected set => _total = value;
        }
    }
}
