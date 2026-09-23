using System;
using System.Collections.Generic;

namespace Shop
{
    public class Store
    {
        private readonly List<string> _items = new List<string>();

        public void Add(string item)
        {
            if (string.IsNullOrEmpty(item))
            {
                throw new InvalidOperationException("Add failed");
            }

            if (_items.Contains(item))
            {
                throw new InvalidOperationException("Add failed");
            }

            _items.Add(item);
        }

        public string Describe(string item)
        {
            try
            {
                Add(item);
                return "Added " + item;
            }
            catch (InvalidOperationException)
            {
                return "Rejected " + item;
            }
        }

        public int Count(string item)
        {
            try
            {
                Add(item);
                return 1;
            }
            catch (InvalidOperationException)
            {
            }
            return 0;
        }
    }
}
