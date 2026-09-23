using System;
using System.Collections.Generic;
using System.Linq;

namespace Reports
{
    public class PrintJob
    {
        private readonly IList<string> _lines;
        private readonly string _title;
        private string? _header;
        private int _count;

        public PrintJob(IList<string> lines, string title)
        {
            _lines = lines;
            _title = title;
        }

        public void Run()
        {
            _header = _title.ToUpperInvariant();
            // Counted first so the header can show it.
            _count = _lines.Count;
            Console.WriteLine(_header + " (" + _count + ")");

            var width = 0;
            _lines.ToList().ForEach(line => width = Math.Max(width, line.Length));
            Console.WriteLine(new string('-', width));
        }
    }
}
