namespace Shop
{
    public class Order
    {
        private int _quantity;
        private decimal _itemPrice;

        public decimal Price
        {
            get
            {
                decimal basePrice = /*[*/_quantity * _itemPrice/*]*/;
                return basePrice * 0.98m;
            }
        }
    }
}
