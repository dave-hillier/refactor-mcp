namespace Shop;

public class Banner
{
    public string Render(string text, int width, char border = '*')
    {
        return text.PadRight(width, border);
    }

    public string Title()
    {
        return Render("Sale", 40) + Render("Clearance", 40, '#');
    }
}
