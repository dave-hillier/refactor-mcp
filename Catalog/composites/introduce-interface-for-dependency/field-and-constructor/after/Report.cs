namespace Shop
{
    public class Report
    {
        // Where the report goes.
        private readonly IWriter _writer;

        public Report(IWriter writer)
        {
            _writer = writer;
        }

        public void Print() => _writer.Write("report");
    }
}
