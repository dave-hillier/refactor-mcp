namespace Shop
{
    public class PrinterAdapter : IPrinter
    {
        private readonly OldPrinter _adaptee;

        public PrinterAdapter(OldPrinter adaptee)
        {
            _adaptee = adaptee;
        }

        public void Print(string text) => _adaptee.Output(text);

        public void Print(string text, int copies) => _adaptee.Output(text, copies);
    }
}
