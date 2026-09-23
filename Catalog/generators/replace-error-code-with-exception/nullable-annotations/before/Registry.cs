using System.Collections.Generic;

namespace Shop
{
    public class Registry
    {
        private readonly HashSet<string> _names = new HashSet<string>();

        public bool Register(string? name)
        {
            if (name is null || !_names.Add(name))
                return false;

            return true;
        }

        public string? Welcome(string? name)
        {
            if (!Register(name))
                return null;

            return "Welcome " + name;
        }
    }
}
