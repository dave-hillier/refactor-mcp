using System;

namespace Shop
{
    public class Counter
    {
        private int _current;

        /// <summary>The current count.</summary>
        [Obsolete("Use Next")]
        public int Value
        {
            get => _current;
            private set => _current = value;
        }

        public int Next() => ++Value;
    }
}
