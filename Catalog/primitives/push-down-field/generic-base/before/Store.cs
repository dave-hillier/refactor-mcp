using System.Collections.Generic;

namespace Shop
{
    public abstract class Store<TItem>
    {
        protected List<TItem> Cache = new List<TItem>();

        public abstract int Count();
    }
}
