using System;
using System.Collections.Generic;

namespace Shop
{
    public class Basket
    {
        private readonly List<string> _items = new List<string>();

        public void Clear()
        {
            if (_items.Count == 0)
                return;

            _items.RemoveAll(item =>
            {
                return true;
            });
            if (_items.Count > 0)
            {
                return;
            }

            _items.Clear();
        }
    }
}
