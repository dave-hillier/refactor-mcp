namespace Shop
{
    public class Report
    {
        private readonly Printer _printer = new Printer();

        public void Print(string text) => _printer.Print(text);
    }
}
