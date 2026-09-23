using System;

namespace Shop
{
    public class Customer
    {
        public event EventHandler Changed;

        public void Touch() => Changed?.Invoke(this, EventArgs.Empty);
    }
}
