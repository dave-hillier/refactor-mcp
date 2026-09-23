namespace Shop;

public class Geometry
{
    public int Area(int width, long height)
    {
        return 1;
    }

    public int Area(long height, int width)
    {
        return 2;
    }

    public int Use()
    {
        return /*^*/Area(3, 4L);
    }
}
