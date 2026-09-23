namespace Shop
{
    public class Report
    {
        public void Print(IWriter writer)
        {
            writer.Write("report");
        }
    }
}
