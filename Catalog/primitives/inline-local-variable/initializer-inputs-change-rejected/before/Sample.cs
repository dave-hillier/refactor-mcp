public class Sample
{
    private int _count;

    public int Reset(int count)
    {
        var /*^*/previous = count;
        count = 0;
        _count = count;
        return previous;
    }
}
