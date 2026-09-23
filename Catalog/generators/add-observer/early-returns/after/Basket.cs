using System;
using System.Collections.Generic;

namespace Shop
{
    public class Basket
    {
        private readonly List<string> _items = new List<string>();

        public event Action Cleared;

        public void Clear()
        {
            if (_items.Count == 0)
            {
                Cleared?.Invoke();
                return;
            }

            _items.RemoveAll(item =>
            {
                return true;
            });
            if (_items.Count > 0)
            {
                Cleared?.Invoke();
                return;
            }

            _items.Clear();
            Cleared?.Invoke();
        }
    }
}
