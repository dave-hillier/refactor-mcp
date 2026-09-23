namespace Shop;

public class Report
{
    public string Title(string name, int width)
    {
        return name.ToUpperInvariant();
    }

    public string Header()
    {
        return Title("sales", 80);
    }
}
