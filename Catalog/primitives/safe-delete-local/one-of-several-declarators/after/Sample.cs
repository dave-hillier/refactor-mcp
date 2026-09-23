public class Sample
{
    private int _next;

    public int Run()
    {
        int first = Next();
        Next();
        int third = Next();
        return first + third;
    }

    private int Next() => ++_next;
}
