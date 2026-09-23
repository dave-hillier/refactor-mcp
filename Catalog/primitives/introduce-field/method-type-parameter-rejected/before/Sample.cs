using System.Collections.Generic;

namespace Shop
{
    public class Sample
    {
        public int Wrap<T>(T item)
        {
            return /*[*/new List<T> { item }/*]*/.Count;
        }
    }
}
