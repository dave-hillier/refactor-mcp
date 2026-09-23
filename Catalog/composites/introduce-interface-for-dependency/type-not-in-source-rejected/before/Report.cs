using System.IO;

namespace Shop
{
    public class Report
    {
        private readonly TextWriter _writer;

        public Report(TextWriter writer)
        {
            _writer = writer;
        }

        public void Print() => _writer.Write("report");
    }
}
