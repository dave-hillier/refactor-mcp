namespace Shop
{
    public class Box<T>
    {
        private T? _item;

        public T? Item
        {
            get => _item;
            set => _item = value;
        }
    }
}
