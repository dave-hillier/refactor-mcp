namespace Shop
{
    public class Box<T>
    {
        private T _item;

        public void Put(T item) => _item = item;

        public T Take() => _item;

        public static Box<T> Empty() => new Box<T>();
    }
}
