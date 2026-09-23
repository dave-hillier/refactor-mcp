using System;
using System.Collections.Generic;

namespace Shop
{
    public class Header
    {
        public string Render(string title, int count)
        {
            // One entry per page.
            var entry = new/*^*/ { Title = title, Count = count }; // shown at the top
            return entry.ToString();
        }
    }

    public class Footer
    {
    }
}
