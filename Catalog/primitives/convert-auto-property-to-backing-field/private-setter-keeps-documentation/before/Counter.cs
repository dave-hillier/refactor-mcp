using System;

namespace Shop
{
    public class Counter
    {
        /// <summary>The current count.</summary>
        [Obsolete("Use Next")]
        public int Value { get; private set; }

        public int Next() => ++Value;
    }
}
