namespace Shop
{
    public partial class Order
    {
        private decimal _total;

        // Discounts never take the total below zero.
        public decimal Discount(decimal amount)
        {
            return amount > _total ? 0 : _total - amount;
        }

        public void Add(decimal price) => _total += price;
    }
}
