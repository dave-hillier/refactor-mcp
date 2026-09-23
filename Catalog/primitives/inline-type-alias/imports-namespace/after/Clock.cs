// Clock helpers.
using System;
using System.Collections.Generic;

namespace Shop
{
    public class Clock
    {
        // The current time.
        public DateTimeOffset Now() => DateTimeOffset.UtcNow;

        public List<DateTimeOffset> History { get; } = new List<DateTimeOffset>();
    }
}
