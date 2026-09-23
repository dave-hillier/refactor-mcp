namespace Shop
{
    public static class Program
    {
        public static void Run()
        {
            var writer = new FileWriter("report.txt");
            new Report(writer).Print();
            writer.Flush();
        }
    }
}
