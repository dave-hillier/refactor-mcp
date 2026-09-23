namespace Shop;

public class Report
{
    public string Title(string name)
    {
        return name.ToUpperInvariant();
    }

    public string Header()
    {
        return Title("sales");
    }
}
