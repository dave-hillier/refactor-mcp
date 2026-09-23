namespace Shop;

public class Alert : Notice
{
    public Alert(string text) : base(text.ToUpperInvariant())
    {
    }
}
