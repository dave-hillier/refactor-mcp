namespace Shop;

public class Report
{
    private int _calls;

    public string Title(string name, int width)
    {
        return name.ToUpperInvariant();
    }

    public string Header()
    {
        return Title("sales", NextWidth());
    }

    private int NextWidth()
    {
        _calls++;
        return 80;
    }
}
