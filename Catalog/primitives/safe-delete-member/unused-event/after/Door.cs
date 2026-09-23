using System;

namespace Shop
{
    public class Door
    {
        public event EventHandler? Closed;

        public void Close() => Closed?.Invoke(this, EventArgs.Empty);
    }
}
