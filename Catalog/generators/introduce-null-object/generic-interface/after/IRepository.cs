using System.Collections.Generic;

namespace Shop;

public interface IRepository<T> where T : class
{
    void Save(T item);

    IReadOnlyList<T> All();

    int Count();
}
