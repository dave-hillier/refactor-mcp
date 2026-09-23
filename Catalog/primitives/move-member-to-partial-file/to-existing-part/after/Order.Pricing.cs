namespace Shop
{
    public partial class Order
    {
        public decimal Total => _total;

        // Discounts never take the total below zero.
        public decimal Discount(decimal amount)
        {
            return amount > _total ? 0 : _total - amount;
        }
    }
}
