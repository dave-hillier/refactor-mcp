using System.Threading;

namespace Shop
{
    public class Counter
    {
        public int Hits;
    }

    public class Tracker
    {
        public void Record(Counter counter) => Interlocked.Increment(ref counter.Hits);
    }
}
