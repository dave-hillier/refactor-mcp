using Shop.Text;

namespace Reports
{
    public class Report
    {
        public string Title(string raw) => Formatting.Trimmed(raw).ToUpperInvariant();

        public int Length(string raw) => Formatting.Trimmed(raw + " ").Length;
    }
}
