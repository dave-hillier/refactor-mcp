using System;

namespace Shop
{
    public class ClockDecorator : IClock
    {
        private readonly IClock _inner;

        public ClockDecorator(IClock inner)
        {
            _inner = inner;
        }

        public DateTime Now => _inner.Now;

        public DateTime UtcNow => _inner.UtcNow;
    }
}
