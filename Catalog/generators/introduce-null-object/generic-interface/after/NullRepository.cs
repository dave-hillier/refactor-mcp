using System;
using System.Collections.Generic;

namespace Shop;

public sealed class NullRepository<T> : IRepository<T> where T : class
{
    public static readonly NullRepository<T> Instance = new NullRepository<T>();

    private NullRepository()
    {
    }

    public void Save(T item)
    {
    }

    public IReadOnlyList<T> All()
    {
        return Array.Empty<T>();
    }

    public int Count()
    {
        return 0;
    }
}
