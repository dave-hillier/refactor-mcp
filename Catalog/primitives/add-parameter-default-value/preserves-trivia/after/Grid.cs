namespace Shop;

public class Grid
{
    public int Rows(
        int width,
        int height = 10 /* in cells */)
    {
        return width * height;
    }
}
