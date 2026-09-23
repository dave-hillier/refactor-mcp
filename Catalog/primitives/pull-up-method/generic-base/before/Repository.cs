using System.Linq;

namespace Shop
{
    public class Repository<T> : Store<T>
    {
        public T First() => Items.First();
    }
}
