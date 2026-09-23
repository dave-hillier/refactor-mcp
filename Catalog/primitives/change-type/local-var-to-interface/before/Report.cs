namespace Shop
{
    public class Report
    {
        public void Print()
        {
            var /*^*/writer = new FileWriter();
            writer.Write("report");
        }
    }
}
