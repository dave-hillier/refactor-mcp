// Clock helpers.
using System.Collections.Generic;
using Stamp = System.DateTimeOffset;

namespace Shop
{
    public class Clock
    {
        // The current time.
        public Stamp Now() => Stamp.UtcNow;

        public List<Stamp> History { get; } = new List<Stamp>();
    }
}
