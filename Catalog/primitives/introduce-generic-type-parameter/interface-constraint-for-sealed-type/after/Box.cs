namespace Shop
{
    public class Box<T> where T : IPriced
    {
        private T _item;

        public void Put(T item) => _item = item;

        public decimal Value() => _item.Total;
    }
}
