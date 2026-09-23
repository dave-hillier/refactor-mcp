namespace Shop
{
    public class Printer
    {
        public string Print(string text) => new System.Text.StringBuilder(text).Append('.').ToString();
    }
}
