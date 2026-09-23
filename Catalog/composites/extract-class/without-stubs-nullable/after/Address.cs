namespace Shop
{
    public class Address
    {
        internal const int MaxLineLength = 40;

        internal string? _street;

        internal string? _town;

        internal string FormatAddress()
        {
            var text = (_street ?? "") + ", " + (_town ?? "");
            return text.Length > MaxLineLength ? text.Substring(0, MaxLineLength) : text;
        }
    }
}
