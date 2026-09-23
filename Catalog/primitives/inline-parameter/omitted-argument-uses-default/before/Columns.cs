namespace Shop;

public class Columns
{
    public string Pad(string text, int width = 10)
    {
        return text.PadLeft(width);
    }

    public string Row()
    {
        return Pad("name") + Pad("price", 10);
    }
}
