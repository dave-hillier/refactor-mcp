namespace Shop
{
    public class Order
    {
        private decimal _base;
        private bool _member;

        public decimal Charge()
        {
            decimal /*^*/price = _base;
            if (_member)
                price = price * 0.9m;
            return price;
        }
    }
}
