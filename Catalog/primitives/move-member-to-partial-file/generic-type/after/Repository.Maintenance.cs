namespace Shop
{
    public partial class Repository<T>
    {
        public void Clear()
        {
            _items.Clear();
        }
    }
}
