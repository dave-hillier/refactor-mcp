using System;
using System.Collections.Generic;
using System.Linq;

namespace Reports
{
    public static class Printer
    {
        public static void Print(IList<string> lines, string title)
        {
            var header = title.ToUpperInvariant();
            // Counted first so the header can show it.
            int count;
            count = lines.Count;
            Console.WriteLine(header + " (" + count + ")");

            var width = 0;
            lines.ToList().ForEach(line => width = Math.Max(width, line.Length));
            Console.WriteLine(new string('-', width));
        }
    }
}
