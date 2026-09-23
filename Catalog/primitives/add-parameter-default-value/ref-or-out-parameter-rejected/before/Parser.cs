namespace Shop;

public class Parser
{
    public bool TryRead(string text, out int value)
    {
        return int.TryParse(text, out value);
    }
}
