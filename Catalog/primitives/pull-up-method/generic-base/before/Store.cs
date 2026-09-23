using System.Collections.Generic;

namespace Shop
{
    public abstract class Store<TItem>
    {
        protected readonly List<TItem> Items = new List<TItem>();
    }
}
