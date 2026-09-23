namespace Shop
{
    public class Printer
    {
        public string Print(object value) => "object";

        public string Show(string value) => "string";

        public string Run() => Print("text");
    }
}
