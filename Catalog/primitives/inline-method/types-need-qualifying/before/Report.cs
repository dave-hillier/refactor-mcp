using System.Text;

namespace Shop
{
    public static class Report
    {
        public static string Line(string text) => new StringBuilder(text).Append('.').ToString();
    }
}
