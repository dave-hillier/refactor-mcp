namespace Shop;

public class Parcel
{
    public Size Box { get; } = new Size(Width: 2m, Height: 3m);

    public decimal Area()
    {
        var (width, height) = Box;
        return width * height;
    }

    public bool IsSquare() => Box == new Size(Box.Width, Height: Box.Width);
}
