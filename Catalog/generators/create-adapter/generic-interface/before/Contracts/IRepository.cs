namespace Shop.Contracts
{
    public interface IRepository<T>
    {
        T Find(int id);

        void Save(T item);
    }
}
