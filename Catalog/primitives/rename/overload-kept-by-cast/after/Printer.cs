namespace Shop
{
    public class Printer
    {
        public string Show(object value) => "object";

        public string Show(string value) => "string";

        public string Run() => Show((object)"text");
    }
}
