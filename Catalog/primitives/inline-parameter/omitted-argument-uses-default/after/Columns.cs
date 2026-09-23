namespace Shop;

public class Columns
{
    public string Pad(string text)
    {
        return text.PadLeft(10);
    }

    public string Row()
    {
        return Pad("name") + Pad("price");
    }
}
