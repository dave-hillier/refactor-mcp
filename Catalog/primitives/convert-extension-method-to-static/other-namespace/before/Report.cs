using Shop.Text;

namespace Reports
{
    public class Report
    {
        public string Title(string raw) => raw.Trimmed().ToUpperInvariant();

        public int Length(string raw) => (raw + " ").Trimmed().Length;
    }
}
