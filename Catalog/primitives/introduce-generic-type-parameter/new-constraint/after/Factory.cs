namespace Shop
{
    public class Factory<T> where T : new()
    {
        public T Create() => new T();
    }
}
