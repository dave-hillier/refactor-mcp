using System.Collections.Generic;

namespace Shop;

public interface IRepository<T> where T : class
{
    void Add(T item);
    IReadOnlyList<T> All();
}
