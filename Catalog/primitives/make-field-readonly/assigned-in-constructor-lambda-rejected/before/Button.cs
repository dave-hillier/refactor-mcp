using System;

namespace Shop
{
    public class Button
    {
        private int _clicks;
        private readonly Action _onClick;

        public Button()
        {
            _onClick = () => _clicks = _clicks + 1;
        }

        public void Click() => _onClick();

        public int Clicks() => _clicks;
    }
}
