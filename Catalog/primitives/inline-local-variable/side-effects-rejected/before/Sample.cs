public class Sample
{
    private int _counter;

    public int Twice()
    {
        var /*^*/next = Next();
        return next + next;
    }

    private int Next() => ++_counter;
}
