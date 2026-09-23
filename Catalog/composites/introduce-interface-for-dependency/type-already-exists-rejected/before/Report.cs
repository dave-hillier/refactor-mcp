namespace Shop
{
    public class Report
    {
        // Where the report goes.
        private readonly FileWriter _writer;

        public Report(FileWriter writer)
        {
            _writer = writer;
        }

        public void Print() => _writer.Write("report");
    }
}
