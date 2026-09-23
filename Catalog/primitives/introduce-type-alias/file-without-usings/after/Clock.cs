// Clock helpers.
using Stamp = System.DateTimeOffset;

namespace Shop
{
    public class Clock
    {
        public Stamp Now() => Stamp.UtcNow;

        public bool IsPast(Stamp when) => when < Now();
    }
}
