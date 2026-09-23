namespace Shop
{
    public class Report
    {
        public IWriter? Writer { get; set; }

        public void Print() => Writer?.Write("report");
    }
}
