public class Sample
{
    public int Total(int w, int h) => Area(w, h) + 1;

    private int Area(int width, int height)
    {
        var area = width * height;
        return area;
    }
}
