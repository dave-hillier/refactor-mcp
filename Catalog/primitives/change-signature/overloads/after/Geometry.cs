namespace Shop;

public class Geometry
{
    public int Area(int side)
    {
        return Area(side, side);
    }

    public int Area(int height, int width)
    {
        return width * height;
    }

    public int Total()
    {
        return Area(3) + Area(5, 4);
    }
}
