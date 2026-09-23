public class Sample
{
    private int _count;

    public int Add(int a)
    {
        var /*^*/sum = _count + a;
        return sum;
    }
}
