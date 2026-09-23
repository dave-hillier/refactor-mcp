namespace Shop
{
    public class Report
    {
        public void Print()
        {
            IWriter writer = new FileWriter();
            writer.Write("report");
        }
    }
}
