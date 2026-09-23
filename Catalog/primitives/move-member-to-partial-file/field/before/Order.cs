namespace Shop
{
    public partial class Order
    {
        private decimal _total, _discount;

        public decimal Net => _total - _discount;
    }
}
