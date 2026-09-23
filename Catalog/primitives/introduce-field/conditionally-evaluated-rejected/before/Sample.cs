namespace Shop
{
    public class Sample
    {
        public bool IsLarge(string text)
        {
            return text != null && /*[*/text.Length > 10/*]*/;
        }
    }
}
