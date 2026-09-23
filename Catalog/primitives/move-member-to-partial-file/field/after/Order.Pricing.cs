namespace Shop
{
    public partial class Order
    {
        public void Discount(decimal amount) => _discount = amount;

        private decimal _discount;
    }
}
