namespace Shop;

public class Parcel
{
    public Size Box { get; } = new Size(Width: 2m, Height: 3m);

    public Size Flat() => new(Box.Width);

    public decimal Area() => Box.Width * Box.Height;
}
