namespace Shop
{
    public class Customer
    {
        private const int MaxLineLength = 40;

        private string? _street;
        private string? _town;

        public string? Postcode { get; set; }

        public void MoveTo(string street, string town)
        {
            _street = street;
            _town = town;
        }

        public string Label() => FormatAddress() + " " + Postcode;

        private string FormatAddress()
        {
            var text = (_street ?? "") + ", " + (_town ?? "");
            return text.Length > MaxLineLength ? text.Substring(0, MaxLineLength) : text;
        }
    }
}
