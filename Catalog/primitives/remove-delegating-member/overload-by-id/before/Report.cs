namespace Shop
{
    public class Report : Printer
    {
        public new void Print(string text) => base.Print(text);

        public new void Print(int number) => base.Print(number);
    }
}
