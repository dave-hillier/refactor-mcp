namespace Shop
{
    public class Report
    {
        private FileWriter _main = new FileWriter(), _backup = new FileWriter();

        public void Print()
        {
            _main.Write("report");
            _main.Flush();
            _backup.Write("report");
        }
    }
}
