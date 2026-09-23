using System;
using System.Collections.Generic;

namespace Shop;

public interface IRepository<T> where T : class
{
    T Find(int id);

    IReadOnlyList<T> All();

    TResult Project<TResult>(T item, Func<T, TResult> map) where TResult : struct;
}
