using System;
using System.Collections.Generic;

namespace Shop;

public class RepositoryDecorator<T> : IRepository<T> where T : class
{
    private readonly IRepository<T> _inner;

    public RepositoryDecorator(IRepository<T> inner)
    {
        _inner = inner;
    }

    public T Find(int id) => _inner.Find(id);

    public IReadOnlyList<T> All() => _inner.All();

    public TResult Project<TResult>(T item, Func<T, TResult> map) where TResult : struct => _inner.Project<TResult>(item, map);
}
