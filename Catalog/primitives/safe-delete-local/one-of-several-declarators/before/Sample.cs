public class Sample
{
    private int _next;

    public int Run()
    {
        int first = Next(), /*^*/second = Next(), third = Next();
        return first + third;
    }

    private int Next() => ++_next;
}
