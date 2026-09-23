using System.Collections.Generic;
using System.Linq;

namespace Shop
{
    public abstract class Store<TItem>
    {
        protected readonly List<TItem> Items = new List<TItem>();

        public TItem First() => Items.First();
    }
}
