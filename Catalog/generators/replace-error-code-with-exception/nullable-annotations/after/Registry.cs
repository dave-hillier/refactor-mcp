using System;
using System.Collections.Generic;

namespace Shop
{
    public class Registry
    {
        private readonly HashSet<string> _names = new HashSet<string>();

        public void Register(string? name)
        {
            if (name is null || !_names.Add(name))
                throw new InvalidOperationException("Register failed");
        }

        public string? Welcome(string? name)
        {
            try
            {
                Register(name);
            }
            catch (InvalidOperationException)
            {
                return null;
            }

            return "Welcome " + name;
        }
    }
}
