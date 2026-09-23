using System.Threading;

namespace Shop
{
    public class Counter
    {
        private int _hits;

        public int Hits
        {
            get => _hits;
            set => _hits = value;
        }

        public void Record() => Interlocked.Increment(ref _hits);
    }
}
