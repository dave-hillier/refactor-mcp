namespace Shop;

public class Parcel
{
    public Size Box { get; } = new Size(width: 2m, height: 3m);

    public decimal Area()
    {
        var (width, height) = Box;
        return width * height;
    }

    public bool IsSquare() => Box == new Size(Box.Width, height: Box.Width);
}
