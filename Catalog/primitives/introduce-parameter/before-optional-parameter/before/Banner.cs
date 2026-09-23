namespace Shop;

public class Banner
{
    public string Render(string text, char border = '*')
    {
        return text.PadRight(/*[*/40/*]*/, border);
    }

    public string Title()
    {
        return Render("Sale") + Render("Clearance", '#');
    }
}
