namespace Shop
{
    public class Label
    {
        public string? Caption { get; set; }

        public string Show() => Caption.OrEmpty();

        public string Blank() => Text.OrEmpty(null);
    }
}
