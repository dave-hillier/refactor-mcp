namespace Shop;

public class Labels
{
    public string Label(string text)
    {
        // upper-case labels shout
        return true ? text.ToUpperInvariant() : text; // chosen by the caller
    }

    public string Banner()
    {
        // both are headings
        return Label("sale") + Label("new");
    }
}
