using System;

namespace Shop
{
    public class Door
    {
        public event EventHandler? Closed;

        public event EventHandler? Opened;

        public void Close() => Closed?.Invoke(this, EventArgs.Empty);
    }
}
