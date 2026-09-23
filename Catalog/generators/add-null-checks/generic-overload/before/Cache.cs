using System;
using System.Collections.Generic;

namespace Shop
{
    public class Cache
    {
        private readonly Dictionary<string, object> _items = new Dictionary<string, object>();

        public void Put(string key, object value)
        {
            _items[key] = value;
        }

        public void Put<TValue, TTag>(string key, TValue value, TTag tag) where TValue : class
        {
            _items[key] = value;
            _items[key + "#tag"] = tag;
        }
    }
}
