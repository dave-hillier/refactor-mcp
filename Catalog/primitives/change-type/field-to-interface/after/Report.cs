namespace Shop
{
    public class Report
    {
        // Where the report goes.
        private readonly IWriter _writer = new FileWriter();

        public void Print() => _writer.Write("report");
    }
}
