namespace Shop
{
    public class Report
    {
        private readonly FileWriter _writer = new FileWriter();

        public void Print()
        {
            _writer.Write("report");
            _writer.Flush();
        }
    }
}
