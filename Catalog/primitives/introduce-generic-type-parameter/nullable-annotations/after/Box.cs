namespace Shop
{
    public class Box<T> where T : Invoice
    {
        private T? _item;

        public void Put(T? item) => _item = item;

        public decimal Value() => _item?.Total ?? 0;
    }
}
