using System;

namespace Shop
{
    public class Names
    {
        public int Length(string? name)
        {
            /*^*/if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return name.Length;
        }
    }
}
