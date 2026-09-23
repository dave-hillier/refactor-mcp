using System;
using System.Collections.Generic;

namespace Shop
{
    public static class Audit
    {
        private static readonly List<string> Entries = new List<string>();

        public static void Record(string entry) => Entries.Add(entry);
    }
}
