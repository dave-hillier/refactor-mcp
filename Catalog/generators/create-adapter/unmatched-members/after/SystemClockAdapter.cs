using System;

namespace Shop
{
    public class SystemClockAdapter : IClock
    {
        private readonly SystemClock _adaptee;

        public SystemClockAdapter(SystemClock adaptee)
        {
            _adaptee = adaptee;
        }

        public DateTime Now => _adaptee.Now;

        public void Freeze(DateTime at) => throw new NotImplementedException();

        public event EventHandler Ticked
        {
            add => throw new NotImplementedException();
            remove => throw new NotImplementedException();
        }
    }
}
