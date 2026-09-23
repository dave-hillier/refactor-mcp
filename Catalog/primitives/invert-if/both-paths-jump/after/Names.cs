using System;

namespace Shop
{
    public class Names
    {
        public int Length(string? name)
        {
            if (name is not null)
            {
                return name.Length;
            }

            throw new ArgumentNullException(nameof(name));
        }
    }
}
