public class Sample
{
    private int _count;
    private const int Limit = 10;

    public bool IsFull()
    {
        return _count >= Limit;
    }
}
