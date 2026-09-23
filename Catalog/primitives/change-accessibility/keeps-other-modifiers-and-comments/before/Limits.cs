namespace Shop;

public class Limits
{
    /// <summary>The largest basket.</summary>
    internal /* shared */ static readonly int Maximum = 3;

    public bool Allows(int count)
    {
        return count <= Maximum;
    }
}
