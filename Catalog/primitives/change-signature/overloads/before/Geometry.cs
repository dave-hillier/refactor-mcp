namespace Shop;

public class Geometry
{
    public int Area(int side)
    {
        return Area(side, side);
    }

    public int Area(int width, int height)
    {
        return width * height;
    }

    public int Total()
    {
        return Area(3) + Area(4, 5);
    }
}
