public class Sample
{
    private int _count;

    public bool IsFull()
    {
        const int /*^*/limit = 10;
        return _count >= limit;
    }
}
