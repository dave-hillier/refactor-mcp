namespace Shop;

public sealed class Size
{
    public Size(decimal width, decimal height = 1m)
    {
        this.Width = width;
        Height = height;
    }

    public decimal Width { get; }

    public decimal Height { get; }
}
