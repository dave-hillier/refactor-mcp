namespace Shop
{
    public partial class Order
    {
        public void AddLine(long price)
        {
            _total += price;
        }
    }
}
