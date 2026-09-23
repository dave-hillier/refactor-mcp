namespace Shop
{
    public class Formatter
    {
        public string Format(string value) => value.Trim();

        public string Format<T>(T value) => value?.ToString() ?? "";

        public string Run() => Format(" a ");
    }
}
