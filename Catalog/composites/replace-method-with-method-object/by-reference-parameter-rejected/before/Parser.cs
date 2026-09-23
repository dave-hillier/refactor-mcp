namespace Shop
{
    public class Parser
    {
        public bool TryParse(string text, out int value)
        {
            value = text.Length;
            return value > 0;
        }
    }
}
