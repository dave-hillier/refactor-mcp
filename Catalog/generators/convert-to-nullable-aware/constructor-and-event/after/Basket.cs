#nullable enable

using System;
using System.Collections.Generic;

namespace Shop
{
    public class Basket
    {
        private readonly List<string> _items;
        private string? _coupon;

        public Basket(IEnumerable<string> items)
        {
            _items = new List<string>(items);
        }

        public event Action? Changed;

        public void Apply(string coupon)
        {
            _coupon = coupon;
            Changed?.Invoke();
        }

        public int CouponLength => _coupon?.Length ?? 0;

        public int Count => _items.Count;
    }
}
