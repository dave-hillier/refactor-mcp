namespace Shop
{
    public class Report
    {
        public FileWriter? Writer { get; set; }

        public void Print() => Writer?.Write("report");
    }
}
