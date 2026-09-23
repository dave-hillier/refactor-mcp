using System.Collections.Generic;

namespace Shop.Data
{
    public abstract class Repository<T>
    {
        protected readonly List<T> Items = new List<T>();
    }
}
