namespace Shop
{
    public class Label
    {
        public string? Caption { get; set; }

        public string Show() => Text.OrEmpty(Caption);

        public string Blank() => Text.OrEmpty(null);
    }
}
