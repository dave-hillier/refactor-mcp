namespace Shop;

public class Labels
{
    public string Label(string text, bool upper)
    {
        // upper-case labels shout
        return upper ? text.ToUpperInvariant() : text; // chosen by the caller
    }

    public string Banner()
    {
        // both are headings
        return Label("sale", true) + Label("new", /* loud */ true);
    }
}
