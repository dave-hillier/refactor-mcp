namespace Shop
{
    public class Box<TItem> where TItem : Invoice
    {
        private TItem _item;

        public void Put(TItem item) => _item = item;

        public decimal Value() => _item.Total;
    }
}
