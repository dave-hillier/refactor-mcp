namespace Shop
{
    public partial class Order
    {
        private decimal _total;

        public decimal Net => _total - _discount;
    }
}
